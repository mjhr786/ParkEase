using FluentAssertions;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Infrastructure.ReadStores;
using ParkingApp.Corporate.Infrastructure.Services;
using ParkingApp.Domain.Enums;
using ParkingApp.Infrastructure.Data;
using ParkingApp.IntegrationTests.Support;
using Xunit;

namespace ParkingApp.IntegrationTests.Corporate;

/// <summary>
/// Real Postgres proof that quota cache + vendor allocation read-store map dual class pools.
/// </summary>
[Collection(PostgresSqlCollection.Name)]
public sealed class DualPoolReadStoreSqlIntegrationTests
{
    private readonly PostgresSqlFixture _fx;

    public DualPoolReadStoreSqlIntegrationTests(PostgresSqlFixture fx) => _fx = fx;

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    [Trait("Layer", "Sql")]
    public async Task CompanyQuotaCache_MapsTwoAndFourWheelerPools()
    {
        await DualPoolSqlSchema.EnsureReadStoreTablesAsync(_fx.ConnectionString);
        var (companyId, _, allocationId) = await SeedDualPoolAllocationAsync();

        var cache = new Mock<ICacheService>();
        cache
            .Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<IReadOnlyList<ParkingApp.Corporate.Application.Interfaces.CompanyQuotaCacheEntry>>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<Task<IReadOnlyList<ParkingApp.Corporate.Application.Interfaces.CompanyQuotaCacheEntry>>> factory, TimeSpan? __, CancellationToken ___) =>
                factory());

        var sql = new NpgsqlConnectionFactory(_fx.ConnectionString);
        var quotaCache = new CompanyQuotaCache(cache.Object, sql);

        var entries = await quotaCache.GetCompanyAllocationsAsync(companyId);
        entries.Should().ContainSingle(e => e.AllocationId == allocationId);

        var entry = entries.Single();
        entry.TotalSlots.Should().Be(30);
        entry.TwoWheeler.Should().NotBeNull();
        entry.TwoWheeler!.TotalSlots.Should().Be(10);
        entry.TwoWheeler.FixedSlots.Should().Be(2);
        entry.TwoWheeler.SharedSlots.Should().Be(8);
        entry.FourWheeler.Should().NotBeNull();
        entry.FourWheeler!.TotalSlots.Should().Be(20);
        entry.FourWheeler.FixedSlots.Should().Be(5);
        entry.FourWheeler.SharedSlots.Should().Be(15);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    [Trait("Layer", "Sql")]
    public async Task VendorAllocations_ExposeDualPools()
    {
        await DualPoolSqlSchema.EnsureReadStoreTablesAsync(_fx.ConnectionString);
        var (companyId, vendorId, allocationId) = await SeedDualPoolAllocationAsync(sourceType: 0 /* VendorLease */);

        var sql = new NpgsqlConnectionFactory(_fx.ConnectionString);
        var readStore = new CompanyReadStore(sql);

        var rows = await readStore.GetVendorAllocationsAsync(vendorId);
        rows.Should().ContainSingle(r => r.Id == allocationId && r.CompanyId == companyId);

        var row = rows.Single();
        row.TwoWheeler.Should().NotBeNull();
        row.TwoWheeler!.TotalSlots.Should().Be(10);
        row.FourWheeler.Should().NotBeNull();
        row.FourWheeler!.TotalSlots.Should().Be(20);
        row.TotalSlots.Should().Be(30);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    [Trait("Layer", "Sql")]
    public async Task LegacyHomogeneousColumns_ReadAsFourWheelerPools_WhenDualZero()
    {
        // Mirrors migration backfill semantics: dual columns may be zero until dual config is used.
        await DualPoolSqlSchema.EnsureReadStoreTablesAsync(_fx.ConnectionString);

        var companyId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        await using var conn = new Npgsql.NpgsqlConnection(_fx.ConnectionString);
        await conn.OpenAsync();
        await ExecAsync(conn, """INSERT INTO "Users" ("Id","FirstName","LastName") VALUES (@id,'V','Owner');""",
            ("id", vendorId));
        await ExecAsync(conn, """INSERT INTO "Companies" ("Id","Name","BillingType") VALUES (@id,'Legacy Co',0);""",
            ("id", companyId));
        await ExecAsync(conn, """
            INSERT INTO "ParkingSpaces" ("Id","Title","HourlyRate","IsActive","OwnerId")
            VALUES (@id,'Legacy Lot',50,TRUE,@owner);
            """, ("id", spaceId), ("owner", vendorId));
        await ExecAsync(conn, """
            INSERT INTO "ParkingAllocations" (
                "Id","CompanyId","ParkingSpaceId",
                "TotalSlots","FixedSlots","SharedSlots",
                "TwoWheelerTotalSlots","TwoWheelerFixedSlots","TwoWheelerSharedSlots",
                "FourWheelerTotalSlots","FourWheelerFixedSlots","FourWheelerSharedSlots",
                "MonthlyRate","StartDate","EndDate","Status","SourceType","VendorId","LeaseReference")
            VALUES (
                @id,@company,@space,
                12,0,12,
                0,0,0,
                0,0,0,
                1000,@start,@end,1,0,@vendor,'LEGACY');
            """,
            ("id", allocationId), ("company", companyId), ("space", spaceId),
            ("start", start), ("end", end), ("vendor", vendorId));

        var sql = new NpgsqlConnectionFactory(_fx.ConnectionString);
        var readStore = new CompanyReadStore(sql);
        var row = (await readStore.GetVendorAllocationsAsync(vendorId)).Single();

        row.TotalSlots.Should().Be(12);
        row.TwoWheeler!.TotalSlots.Should().Be(0);
        row.FourWheeler!.TotalSlots.Should().Be(0);
    }

    private async Task<(Guid CompanyId, Guid VendorId, Guid AllocationId)> SeedDualPoolAllocationAsync(
        int sourceType = 1 /* CompanyOwned default for quota path */)
    {
        var companyId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        await using var conn = new Npgsql.NpgsqlConnection(_fx.ConnectionString);
        await conn.OpenAsync();
        await ExecAsync(conn, """INSERT INTO "Users" ("Id","FirstName","LastName") VALUES (@id,'Ven','Dor');""",
            ("id", vendorId));
        await ExecAsync(conn, """INSERT INTO "Companies" ("Id","Name","BillingType") VALUES (@id,'Acme Dual',0);""",
            ("id", companyId));
        await ExecAsync(conn, """
            INSERT INTO "ParkingSpaces" ("Id","Title","HourlyRate","IsActive","OwnerId")
            VALUES (@id,'HQ Lot',40,TRUE,@owner);
            """, ("id", spaceId), ("owner", vendorId));
        await ExecAsync(conn, """
            INSERT INTO "ParkingAllocations" (
                "Id","CompanyId","ParkingSpaceId",
                "TotalSlots","FixedSlots","SharedSlots",
                "TwoWheelerTotalSlots","TwoWheelerFixedSlots","TwoWheelerSharedSlots",
                "FourWheelerTotalSlots","FourWheelerFixedSlots","FourWheelerSharedSlots",
                "MonthlyRate","StartDate","EndDate","Status","SourceType","VendorId","LeaseReference",
                "MaxBookingsPerDay","MaxBookingsPerWeek","PriorityThreshold",
                "AllowedStartTime","AllowedEndTime","AllowWeekends")
            VALUES (
                @id,@company,@space,
                30,7,23,
                10,2,8,
                20,5,15,
                0,@start,@end,1,@source,@vendor,'DUAL-1',
                10,40,1,
                INTERVAL '0 hours', INTERVAL '23 hours', TRUE);
            """,
            ("id", allocationId), ("company", companyId), ("space", spaceId),
            ("start", start), ("end", end), ("source", sourceType), ("vendor", vendorId));

        return (companyId, vendorId, allocationId);
    }

    private static async Task ExecAsync(Npgsql.NpgsqlConnection conn, string sql, params (string Name, object Value)[] args)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }
}
