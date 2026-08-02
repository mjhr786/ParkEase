using Dapper;
using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.IntegrationTests.Support;
using Xunit;

namespace ParkingApp.IntegrationTests.Corporate;

/// <summary>
/// Real Postgres (Testcontainers) proof of class-scoped shared occupancy SQL
/// (same predicates as CorporateBookingRepository.GetReservationPreCheckAsync).
/// Uses Dapper against a minimal schema so we do not require full EF/PostGIS model bootstrap.
/// </summary>
[Collection(PostgresSqlCollection.Name)]
public sealed class VehicleClassOccupancySqlIntegrationTests
{
    private readonly PostgresSqlFixture _fx;

    public VehicleClassOccupancySqlIntegrationTests(PostgresSqlFixture fx) => _fx = fx;

    /// <summary>
    /// Subset of production pre-check SQL focused on class-scoped shared occupancy.
    /// Keep filter predicates aligned with CompanyRepository/CorporateBookingRepository.
    /// </summary>
    private const string ClassScopedOccupancySql = """
        SELECT CAST((
            SELECT COUNT(*)
            FROM "CorporateBookings" cb
            INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
            WHERE cb."CompanyId" = @CompanyId
              AND cb."AllocationId" = @AllocationId
              AND cb."SlotType" = 1
              AND cb."IsDeleted" = FALSE
              AND b."IsDeleted" = FALSE
              AND b."StartDateTime" < @WindowEnd
              AND b."EndDateTime" > @WindowStart
              AND b."Status" NOT IN (4, 5, 7)
              AND (
                    (@IsTwoWheeler = TRUE AND b."VehicleType" = 1)
                    OR (@IsTwoWheeler = FALSE AND (b."VehicleType" IS NULL OR b."VehicleType" <> 1))
              )
        ) AS INTEGER) AS ActiveSharedBookingCount;

        SELECT DISTINCT b."SlotNumber" AS SlotNumber
        FROM "CorporateBookings" cb
        INNER JOIN "Bookings" b ON b."Id" = cb."BookingId"
        WHERE cb."CompanyId" = @CompanyId
          AND cb."AllocationId" = @AllocationId
          AND cb."SlotType" = 1
          AND cb."IsDeleted" = FALSE
          AND b."IsDeleted" = FALSE
          AND b."SlotNumber" IS NOT NULL
          AND b."StartDateTime" < @WindowEnd
          AND b."EndDateTime" > @WindowStart
          AND b."Status" NOT IN (4, 5, 7)
          AND (
                (@IsTwoWheeler = TRUE AND b."VehicleType" = 1)
                OR (@IsTwoWheeler = FALSE AND (b."VehicleType" IS NULL OR b."VehicleType" <> 1))
          );
        """;

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    [Trait("Layer", "Sql")]
    public async Task PreCheck_ScopesSharedOccupancy_ByVehicleClass()
    {
        await DualPoolSqlSchema.EnsureOccupancyTablesAsync(_fx.ConnectionString);

        var companyId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var windowStart = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var windowEnd = windowStart.AddHours(2);

        await SeedSharedBookingAsync(companyId, allocationId, membershipId,
            vehicleType: 1 /* Motorcycle */, slotNumber: 1, windowStart, windowEnd);
        await SeedSharedBookingAsync(companyId, allocationId, membershipId,
            vehicleType: 2 /* Car */, slotNumber: 1, windowStart, windowEnd);
        await SeedSharedBookingAsync(companyId, allocationId, membershipId,
            vehicleType: null, slotNumber: 2, windowStart, windowEnd);

        var twoW = await QueryOccupancyAsync(companyId, allocationId, windowStart, windowEnd, VehicleClass.TwoWheeler);
        var fourW = await QueryOccupancyAsync(companyId, allocationId, windowStart, windowEnd, VehicleClass.FourWheeler);

        twoW.Count.Should().Be(1, "only Motorcycle counts as 2W");
        twoW.Slots.Should().BeEquivalentTo(new[] { 1 });

        fourW.Count.Should().Be(2, "Car + null VehicleType count as 4W");
        fourW.Slots.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    [Trait("Layer", "Sql")]
    public async Task PreCheck_IgnoresTerminalStatuses_AndOtherAllocations()
    {
        await DualPoolSqlSchema.EnsureOccupancyTablesAsync(_fx.ConnectionString);

        var companyId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var otherAllocation = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var windowStart = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var windowEnd = windowStart.AddHours(2);

        await SeedSharedBookingAsync(companyId, allocationId, membershipId,
            vehicleType: 1, slotNumber: 1, windowStart, windowEnd, status: 1);
        await SeedSharedBookingAsync(companyId, allocationId, membershipId,
            vehicleType: 1, slotNumber: 2, windowStart, windowEnd, status: 4);
        await SeedSharedBookingAsync(companyId, otherAllocation, membershipId,
            vehicleType: 1, slotNumber: 3, windowStart, windowEnd, status: 1);

        var twoW = await QueryOccupancyAsync(companyId, allocationId, windowStart, windowEnd, VehicleClass.TwoWheeler);

        twoW.Count.Should().Be(1);
        twoW.Slots.Should().BeEquivalentTo(new[] { 1 });
    }

    private async Task<(int Count, List<int> Slots)> QueryOccupancyAsync(
        Guid companyId,
        Guid allocationId,
        DateTime windowStart,
        DateTime windowEnd,
        VehicleClass vehicleClass)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_fx.ConnectionString);
        await conn.OpenAsync();
        await using var multi = await conn.QueryMultipleAsync(ClassScopedOccupancySql, new
        {
            CompanyId = companyId,
            AllocationId = allocationId,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            IsTwoWheeler = vehicleClass == VehicleClass.TwoWheeler
        });

        var count = await multi.ReadSingleAsync<int>();
        var slots = (await multi.ReadAsync<int?>())
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToList();
        return (count, slots);
    }

    private async Task SeedSharedBookingAsync(
        Guid companyId,
        Guid allocationId,
        Guid membershipId,
        int? vehicleType,
        int slotNumber,
        DateTime start,
        DateTime end,
        int status = 1)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_fx.ConnectionString);
        await conn.OpenAsync();
        var bookingId = Guid.NewGuid();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO "Bookings" ("Id","StartDateTime","EndDateTime","Status","IsDeleted","SlotNumber","VehicleType","VehicleNumber")
                VALUES (@id, @start, @end, @status, FALSE, @slot, @vt, 'TEST-1');
                """;
            cmd.Parameters.AddWithValue("id", bookingId);
            cmd.Parameters.AddWithValue("start", start);
            cmd.Parameters.AddWithValue("end", end);
            cmd.Parameters.AddWithValue("status", status);
            cmd.Parameters.AddWithValue("slot", slotNumber);
            cmd.Parameters.AddWithValue("vt", (object?)vehicleType ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO "CorporateBookings" ("Id","CompanyId","AllocationId","MembershipId","BookingId","SlotType","IsDeleted")
                VALUES (@id, @company, @alloc, @member, @booking, 1, FALSE);
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("company", companyId);
            cmd.Parameters.AddWithValue("alloc", allocationId);
            cmd.Parameters.AddWithValue("member", membershipId);
            cmd.Parameters.AddWithValue("booking", bookingId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
