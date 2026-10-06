using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NetTopologySuite.Geometries;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Models;
using ParkingApp.Marketplace.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.IntegrationTests.Marketplace;

/// <summary>
/// Test-only. Executes BookingRepository.GetDiscoveryBookingAvailabilityAsync on the existing
/// PostGIS Testcontainer. No production behavior is changed by this file.
/// </summary>
[Collection(FullApiHttpCollection.Name)]
[Trait("Layer", "PostgreSQL")]
[Trait("Feature", "DiscoveryBookingAvailability")]
public sealed class DiscoveryBookingAvailabilityProjectionTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly SqlCapture _sql = new();
    private readonly List<Guid> _spaceIds = new();
    private ApplicationDbContext? _db;
    private BookingRepository? _repository;
    private Guid _userId;

    public DiscoveryBookingAvailabilityProjectionTests(FullApiPostgresFixture postgres) =>
        _connectionString = postgres.ConnectionString;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.CommandTimeout(30);
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name);
            })
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(_sql)
            .Options;

        _db = new ApplicationDbContext(options);
        await _db.Database.MigrateAsync();
        _repository = new BookingRepository(_db);
    }

    public async Task DisposeAsync()
    {
        if (_db is not null)
        {
            if (_spaceIds.Count > 0)
            {
                await _db.Bookings.IgnoreQueryFilters()
                    .Where(b => _spaceIds.Contains(b.ParkingSpaceId))
                    .ExecuteDeleteAsync();
                await _db.ParkingSpaces.IgnoreQueryFilters()
                    .Where(p => _spaceIds.Contains(p.Id))
                    .ExecuteDeleteAsync();
            }

            if (_userId != Guid.Empty)
            {
                await _db.Users.IgnoreQueryFilters()
                    .Where(u => u.Id == _userId)
                    .ExecuteDeleteAsync();
            }

            await _db.DisposeAsync();
        }
    }

    [Fact]
    public async Task DiscoveryQuery_ProjectsFiveColumns_AndMatchesFullBookingAvailability()
    {
        var db = _db!;
        var repository = _repository!;
        // timestamptz stores microseconds. Trim the 100ns digit so the round-trip matches.
        var now = DateTime.UtcNow;
        now = new DateTime(now.Ticks - (now.Ticks % 10), DateTimeKind.Utc);
        var user = User.Register(
            $"p1-{Guid.NewGuid():N}@example.com",
            "hash",
            "P1",
            "Projection",
            "9000000000");
        _userId = user.Id;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var spaceA = AddSpace(db, "A");
        var spaceB = AddSpace(db, "B");
        var spaceC = AddSpace(db, "C");
        var spaceEmpty = AddSpace(db, "Empty");
        await db.SaveChangesAsync();

        var included = new List<BookingAvailabilityRead>();
        void Include(Guid spaceId, BookingStatus status, DateTime start, DateTime end, int? slot)
        {
            db.Bookings.Add(Make(user.Id, spaceId, status, start, end, slot, deleted: false));
            included.Add(new BookingAvailabilityRead(spaceId, status, start, end, slot));
        }

        Include(spaceA, BookingStatus.Confirmed, now.AddMinutes(-30), now.AddHours(2), 1);
        Include(spaceA, BookingStatus.InProgress, now.AddMinutes(-10), now.AddHours(3), 2);
        Include(spaceA, BookingStatus.Pending, now.AddHours(3), now.AddHours(4), null);
        Include(spaceA, BookingStatus.AwaitingPayment, now.AddHours(4), now.AddHours(5), 4);
        Include(spaceA, BookingStatus.PendingExtension, now.AddHours(5), now.AddHours(6), 5);
        Include(spaceA, BookingStatus.AwaitingExtensionPayment, now.AddHours(6), now.AddHours(7), 6);
        Include(spaceB, BookingStatus.Confirmed, now.AddMinutes(-15), now.AddHours(2), 7);

        db.Bookings.Add(Make(user.Id, spaceA, BookingStatus.Completed, now.AddHours(1), now.AddHours(2), 8, false));
        db.Bookings.Add(Make(user.Id, spaceA, BookingStatus.Cancelled, now.AddHours(1), now.AddHours(2), 8, false));
        db.Bookings.Add(Make(user.Id, spaceA, BookingStatus.Expired, now.AddHours(1), now.AddHours(2), 8, false));
        db.Bookings.Add(Make(user.Id, spaceA, BookingStatus.Rejected, now.AddHours(1), now.AddHours(2), 8, false));
        db.Bookings.Add(Make(user.Id, spaceA, BookingStatus.Confirmed, now.AddHours(-3), now.AddMinutes(-2), 8, false));
        db.Bookings.Add(Make(user.Id, spaceA, BookingStatus.Confirmed, now.AddMinutes(-20), now.AddHours(2), 9, true));
        db.Bookings.Add(Make(user.Id, spaceC, BookingStatus.Confirmed, now.AddMinutes(-20), now.AddHours(2), 10, false));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var requested = new[] { spaceA, spaceB, spaceEmpty };
        _sql.Commands.Clear();
        var projected = await repository.GetDiscoveryBookingAvailabilityAsync(requested);
        var projectionSql = string.Join("\n---\n", _sql.Commands);
        db.ChangeTracker.Entries<Booking>().Should().BeEmpty();

        _sql.Commands.Clear();
        var full = (await repository.GetActiveBookingsForSpacesAsync(requested)).ToList();
        var fullSql = string.Join("\n---\n", _sql.Commands);
        db.ChangeTracker.Entries<Booking>().Should().BeEmpty();

        var sqlPath = Path.Combine(Path.GetTempPath(), "p1-booking-availability-sql.txt");
        await File.WriteAllTextAsync(sqlPath, "PROJECTION\n" + projectionSql + "\n\nFULL\n" + fullSql);

        projected.Should().BeEquivalentTo(included);
        projected.Should().OnlyContain(row => requested.Contains(row.ParkingSpaceId));
        projected.Should().NotContain(row => row.ParkingSpaceId == spaceC);
        projected.Should().NotContain(row => row.SlotNumber == 8 || row.SlotNumber == 9 || row.SlotNumber == 10);
        projected.Where(row => row.ParkingSpaceId == spaceA).Should().HaveCount(6);
        projected.Where(row => row.ParkingSpaceId == spaceB).Should().ContainSingle();
        projected.Where(row => row.ParkingSpaceId == spaceEmpty).Should().BeEmpty();
        full.Select(Read).Should().BeEquivalentTo(projected);

        var spaceAOnly = await repository.GetDiscoveryBookingAvailabilityAsync(new[] { spaceA });
        spaceAOnly.Should().BeEquivalentTo(included.Where(row => row.ParkingSpaceId == spaceA));
        (await repository.GetDiscoveryBookingAvailabilityAsync(Array.Empty<Guid>())).Should().BeEmpty();
        (await repository.GetActiveBookingsForSpacesAsync(Array.Empty<Guid>())).Should().BeEmpty();

        var asOf = DateTime.UtcNow;
        var space = await db.ParkingSpaces.AsNoTracking().SingleAsync(p => p.Id == spaceA);
        var fromProjection = space.ToDtoWithFullDetails(
            projected.Where(row => row.ParkingSpaceId == spaceA).ToList(),
            priceAsOfUtc: asOf);
        var fromEntities = space.ToDtoWithFullDetails(
            full.Where(b => b.ParkingSpaceId == spaceA).ToList(),
            priceAsOfUtc: asOf);
        var withNone = space.ToDtoWithFullDetails(Array.Empty<BookingAvailabilityRead>(), priceAsOfUtc: asOf);

        fromProjection.ActiveReservations.Should().BeEquivalentTo(
            fromEntities.ActiveReservations, options => options.WithStrictOrdering());
        fromProjection.EffectiveHourlyRate.Should().Be(fromEntities.EffectiveHourlyRate);
        fromProjection.EffectiveHourlyRate.Should().NotBe(withNone.EffectiveHourlyRate);
        fromProjection.ActiveReservations!.Select(r => r.SlotNumber).Should().Equal(new int?[] { 1, 2, null, 4, 5, 6 });
        fromProjection.ActiveReservations.Should().OnlyContain(r => r.UserName == null);

        var emptySpace = await db.ParkingSpaces.AsNoTracking().SingleAsync(p => p.Id == spaceEmpty);
        var emptyDto = emptySpace.ToDtoWithFullDetails(
            await repository.GetDiscoveryBookingAvailabilityAsync(new[] { spaceEmpty }),
            priceAsOfUtc: asOf);
        emptyDto.ActiveReservations.Should().BeEmpty();

        var select = SelectList(projectionSql);
        select.Should().Contain("\"ParkingSpaceId\"");
        select.Should().Contain("\"Status\"");
        select.Should().Contain("\"StartDateTime\"");
        select.Should().Contain("\"EndDateTime\"");
        select.Should().Contain("\"SlotNumber\"");
        select.Should().NotContain("\"QRCode\"");
        select.Should().NotContain("\"UserId\"");
        select.Should().NotContain("\"TotalAmount\"");
        select.Should().NotContain("\"BaseAmount\"");
        select.Should().NotContain("\"BookingReference\"");
        select.Should().NotContain("\"VehicleNumber\"");
        select.Should().NotContain("\"VehicleModel\"");
        select.Should().NotContain("\"CancellationReason\"");
        select.Should().NotContain("\"Id\"");
        projectionSql.Should().Contain("\"IsDeleted\"");
        projectionSql.Should().NotContain("JOIN");
        projectionSql.Should().NotContain("ORDER BY");
        projectionSql.Should().NotContain("CompanyId");

        var fullSelect = SelectList(fullSql);
        fullSelect.Should().Contain("\"QRCode\"");
        fullSelect.Should().Contain("\"UserId\"");
        fullSelect.Should().Contain("\"TotalAmount\"");
        fullSelect.Should().Contain("\"BookingReference\"");
        fullSelect.Should().Contain("\"VehicleNumber\"");
        fullSql.Should().Contain("\"IsDeleted\"");
        fullSql.Should().NotContain("JOIN");
    }

    private Guid AddSpace(ApplicationDbContext db, string name)
    {
        var space = new ParkingSpace
        {
            OwnerId = _userId,
            Title = "P1 " + name,
            Description = "Booking availability projection",
            Address = "1 Projection Road",
            City = "P1" + Guid.NewGuid().ToString("N")[..10],
            State = "MH",
            Country = "IN",
            PostalCode = "411001",
            Latitude = 18.52,
            Longitude = 73.85,
            Location = new Point(73.85, 18.52) { SRID = 4326 },
            TotalSpots = 4,
            AvailableSpots = 4,
            HourlyRate = 100m,
            DailyRate = 800m,
            WeeklyRate = 4000m,
            MonthlyRate = 16000m,
            IsActive = true,
            IsDynamicPricingEnabled = true,
            TimeZoneId = "UTC"
        };
        db.ParkingSpaces.Add(space);
        _spaceIds.Add(space.Id);
        return space.Id;
    }

    private static Booking Make(
        Guid userId,
        Guid spaceId,
        BookingStatus status,
        DateTime start,
        DateTime end,
        int? slot,
        bool deleted)
    {
        var booking = Booking.CreateMarketplace(
            userId,
            spaceId,
            start,
            end,
            PricingType.Hourly,
            VehicleType.Car,
            baseAmount: 100m,
            taxAmount: 18m,
            serviceFee: 5m,
            discountAmount: 0m,
            totalAmount: 123m,
            slotNumber: slot,
            vehicleNumber: "MH12AB1234",
            vehicleModel: "Wide-Model",
            vehicleColor: "Blue",
            bookingReference: "P1" + Guid.NewGuid().ToString("N"));
        booking.Status = status;
        booking.QRCode = new string('Q', 400);
        booking.CancellationReason = "unused-by-discovery";
        booking.IsDeleted = deleted;
        return booking;
    }

    private static BookingAvailabilityRead Read(Booking booking) =>
        new(booking.ParkingSpaceId, booking.Status, booking.StartDateTime, booking.EndDateTime, booking.SlotNumber);

    private static string SelectList(string sql)
    {
        var from = sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
        return from < 0 ? sql : sql[..from];
    }

    private sealed class SqlCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = new();

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
