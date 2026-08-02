using FluentAssertions;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Allocations;
using ParkingApp.Application.CQRS.Commands.Corporate.Bookings;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.IntegrationTests.Support;
using Xunit;

namespace ParkingApp.IntegrationTests.Corporate;

/// <summary>
/// Multi-step application-layer integration for corporate 2W/4W separate slot pools (S1–S5).
/// </summary>
public class VehicleClassPoolLifecycleIntegrationTests
{
    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public async Task S1_HappyDualConfigure_AndDualBook()
    {
        var fx = new InMemoryCorporateFixture();
        var company = fx.SeedCompany();
        var space = fx.SeedOwnedSpace(company.Id, totalSpots: 30);
        var (start, end) = InMemoryCorporateFixture.WeekdayWindow();

        var create = await fx.CreateOwnedHandler().HandleAsync(new CreateOwnedParkingAllocationCommand(
            company.Id, fx.AdminId,
            new CreateOwnedParkingAllocationDto(
                ParkingSpaceId: space.ParkingSpaceId,
                MonthlyRate: 0m,
                StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                Policy: new BookingPolicyDto(10, 40, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), true),
                TwoWheeler: new SlotPoolDto(10, 0, 10),
                FourWheeler: new SlotPoolDto(20, 0, 20))));

        create.Success.Should().BeTrue(create.Message);
        create.Data!.TwoWheeler!.TotalSlots.Should().Be(10);
        create.Data.FourWheeler!.TotalSlots.Should().Be(20);
        create.Data.TotalSlots.Should().Be(30);
        var allocationId = create.Data.Id;

        var bike = await fx.BookEmployeeHandler().HandleAsync(new BookCorporateParkingCommand(
            company.Id, fx.EmployeeAId,
            new BookCorporateParkingDto(allocationId, start, end, VehicleType.Motorcycle, "KA01BIKE")));
        bike.Success.Should().BeTrue(bike.Message);
        bike.Data!.Booking.Should().NotBeNull();
        fx.TrackSharedOccupancy(allocationId, VehicleClass.TwoWheeler, 1, bike.Data.Booking!.BookingId);

        var car = await fx.BookEmployeeHandler().HandleAsync(new BookCorporateParkingCommand(
            company.Id, fx.EmployeeBId,
            new BookCorporateParkingDto(allocationId, start, end, VehicleType.Car, "KA01CAR1")));
        car.Success.Should().BeTrue(car.Message);
        car.Data!.Booking.Should().NotBeNull();

        company.CorporateBookings.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public async Task S2_CrossPoolIsolation_UnderContention()
    {
        var fx = new InMemoryCorporateFixture();
        var company = fx.SeedCompany();
        var space = fx.SeedOwnedSpace(company.Id, totalSpots: 10);
        var (start, end) = InMemoryCorporateFixture.WeekdayWindow();

        var create = await fx.CreateOwnedHandler().HandleAsync(new CreateOwnedParkingAllocationCommand(
            company.Id, fx.AdminId,
            new CreateOwnedParkingAllocationDto(
                ParkingSpaceId: space.ParkingSpaceId,
                MonthlyRate: 0m,
                StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                Policy: new BookingPolicyDto(10, 40, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), true),
                TwoWheeler: new SlotPoolDto(1, 0, 1),
                FourWheeler: new SlotPoolDto(5, 0, 5))));
        create.Success.Should().BeTrue(create.Message);
        var allocationId = create.Data!.Id;

        var firstBike = await fx.BookEmployeeHandler().HandleAsync(new BookCorporateParkingCommand(
            company.Id, fx.EmployeeAId,
            new BookCorporateParkingDto(allocationId, start, end, VehicleType.Motorcycle, "BIKE1")));
        firstBike.Success.Should().BeTrue(firstBike.Message);
        firstBike.Data!.Booking.Should().NotBeNull();
        fx.TrackSharedOccupancy(allocationId, VehicleClass.TwoWheeler, 1, firstBike.Data.Booking!.BookingId);

        var secondBike = await fx.BookEmployeeHandler().HandleAsync(new BookCorporateParkingCommand(
            company.Id, fx.EmployeeBId,
            new BookCorporateParkingDto(allocationId, start, end, VehicleType.Motorcycle, "BIKE2")));
        secondBike.Success.Should().BeTrue(secondBike.Message);
        secondBike.Message.Should().Contain("waitlist");
        secondBike.Data!.Waitlist.Should().NotBeNull();
        secondBike.Data.Booking.Should().BeNull();

        var car = await fx.BookEmployeeHandler().HandleAsync(new BookCorporateParkingCommand(
            company.Id, fx.EmployeeBId,
            new BookCorporateParkingDto(allocationId, start, end, VehicleType.Car, "CAR1")));
        car.Success.Should().BeTrue(car.Message);
        car.Data!.Booking.Should().NotBeNull("4W must remain bookable when only 2W is full");
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public async Task S3_LegacyApi_MapsToFourWheelerOnly()
    {
        var fx = new InMemoryCorporateFixture();
        var company = fx.SeedCompany();
        var space = fx.SeedVendorSpace(totalSpots: 20);
        var (start, end) = InMemoryCorporateFixture.WeekdayWindow();

        var allocate = await fx.AllocateHandler().HandleAsync(new AllocateParkingSlotsCommand(
            company.Id, fx.AdminId,
            new AllocateParkingSlotsDto(
                space.ParkingSpaceId, 5, 0, 5, 1000m,
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                "LEGACY-1",
                new BookingPolicyDto(10, 40, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), true))));

        allocate.Success.Should().BeTrue(allocate.Message);
        allocate.Data!.FourWheeler!.TotalSlots.Should().Be(5);
        allocate.Data.TwoWheeler!.TotalSlots.Should().Be(0);

        // Vendor allocations start Pending — activate via domain for book path
        var allocation = company.Allocations.Single(a => a.Id == allocate.Data.Id);
        allocation.Approve(space.OwnerId);

        var bike = await fx.BookEmployeeHandler().HandleAsync(new BookCorporateParkingCommand(
            company.Id, fx.EmployeeAId,
            new BookCorporateParkingDto(allocation.Id, start, end, VehicleType.Motorcycle, "BIKE")));
        bike.Success.Should().BeFalse();
        bike.Message.Should().Contain("2-wheeler");

        var car = await fx.BookEmployeeHandler().HandleAsync(new BookCorporateParkingCommand(
            company.Id, fx.EmployeeAId,
            new BookCorporateParkingDto(allocation.Id, start, end, VehicleType.Car, "CAR")));
        car.Success.Should().BeTrue(car.Message);
        car.Data!.Booking.Should().NotBeNull();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public async Task S4_FixedBays_PerClass()
    {
        var fx = new InMemoryCorporateFixture();
        var company = fx.SeedCompany();
        var space = fx.SeedOwnedSpace(company.Id, totalSpots: 10);
        var (start, end) = InMemoryCorporateFixture.WeekdayWindow();

        var create = await fx.CreateOwnedHandler().HandleAsync(new CreateOwnedParkingAllocationCommand(
            company.Id, fx.AdminId,
            new CreateOwnedParkingAllocationDto(
                ParkingSpaceId: space.ParkingSpaceId,
                MonthlyRate: 0m,
                StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                Policy: new BookingPolicyDto(10, 40, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), true),
                TwoWheeler: new SlotPoolDto(2, 1, 1),
                FourWheeler: new SlotPoolDto(2, 1, 1))));
        create.Success.Should().BeTrue(create.Message);
        var allocationId = create.Data!.Id;
        var memberA = company.Memberships.First(m => m.UserId == fx.EmployeeAId);

        var assign2 = await fx.AssignFixedHandler().HandleAsync(new AssignFixedSlotCommand(
            company.Id, allocationId, fx.AdminId,
            new AssignFixedSlotDto(memberA.Id, 1, VehicleClass.TwoWheeler)));
        var assign4 = await fx.AssignFixedHandler().HandleAsync(new AssignFixedSlotCommand(
            company.Id, allocationId, fx.AdminId,
            new AssignFixedSlotDto(memberA.Id, 1, VehicleClass.FourWheeler)));
        assign2.Success.Should().BeTrue(assign2.Message);
        assign4.Success.Should().BeTrue(assign4.Message);

        var bike = await fx.BookEmployeeHandler().HandleAsync(new BookCorporateParkingCommand(
            company.Id, fx.EmployeeAId,
            new BookCorporateParkingDto(allocationId, start, end, VehicleType.Motorcycle, "BIKE")));
        bike.Success.Should().BeTrue(bike.Message);
        bike.Data!.Booking.Should().NotBeNull();

        // Second booking same member same window blocked by fraud/overlap if pre-check says overlap —
        // use Employee B for car on their own fixed is not assigned; book employee A car after clearing overlap flags
        // Domain fraud uses pre-check HasOverlappingMemberBooking which we keep false in fixture.
        var car = await fx.BookEmployeeHandler().HandleAsync(new BookCorporateParkingCommand(
            company.Id, fx.EmployeeAId,
            new BookCorporateParkingDto(allocationId, start.AddHours(3), end.AddHours(3), VehicleType.Car, "CAR")));
        car.Success.Should().BeTrue(car.Message);
        car.Data!.Booking.Should().NotBeNull();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public async Task S5_VisitorClassConsumption()
    {
        var fx = new InMemoryCorporateFixture();
        var company = fx.SeedCompany();
        var space = fx.SeedOwnedSpace(company.Id, totalSpots: 10);
        var (start, end) = InMemoryCorporateFixture.WeekdayWindow();

        var create = await fx.CreateOwnedHandler().HandleAsync(new CreateOwnedParkingAllocationCommand(
            company.Id, fx.AdminId,
            new CreateOwnedParkingAllocationDto(
                ParkingSpaceId: space.ParkingSpaceId,
                MonthlyRate: 0m,
                StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                Policy: new BookingPolicyDto(10, 40, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), true),
                TwoWheeler: new SlotPoolDto(1, 0, 1),
                FourWheeler: new SlotPoolDto(3, 0, 3))));
        create.Success.Should().BeTrue(create.Message);
        var allocationId = create.Data!.Id;

        var v1 = await fx.BookVisitorHandler().HandleAsync(new BookVisitorParkingCommand(
            company.Id, fx.EmployeeAId,
            new BookVisitorParkingDto(allocationId, start, end, "Guest Bike", "VISBIKE1", end.AddHours(1),
                VehicleType.Motorcycle)));
        v1.Success.Should().BeTrue(v1.Message);
        v1.Data!.Booking.Should().NotBeNull();
        fx.TrackSharedOccupancy(allocationId, VehicleClass.TwoWheeler, 1, v1.Data.Booking!.BookingId);

        var v2 = await fx.BookVisitorHandler().HandleAsync(new BookVisitorParkingCommand(
            company.Id, fx.EmployeeAId,
            new BookVisitorParkingDto(allocationId, start, end, "Guest Bike 2", "VISBIKE2", end.AddHours(1),
                VehicleType.Motorcycle)));
        v2.Success.Should().BeTrue(v2.Message);
        v2.Message.Should().Contain("waitlist");
        v2.Data!.Waitlist.Should().NotBeNull();

        var vCar = await fx.BookVisitorHandler().HandleAsync(new BookVisitorParkingCommand(
            company.Id, fx.EmployeeBId,
            new BookVisitorParkingDto(allocationId, start, end, "Guest Car", "VISCAR1", end.AddHours(1),
                VehicleType.Car)));
        vCar.Success.Should().BeTrue(vCar.Message);
        vCar.Data!.Booking.Should().NotBeNull();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public async Task DualPools_ExceedCapacity_RejectedAtHandler()
    {
        var fx = new InMemoryCorporateFixture();
        var company = fx.SeedCompany();
        var space = fx.SeedOwnedSpace(company.Id, totalSpots: 30);

        var create = await fx.CreateOwnedHandler().HandleAsync(new CreateOwnedParkingAllocationCommand(
            company.Id, fx.AdminId,
            new CreateOwnedParkingAllocationDto(
                ParkingSpaceId: space.ParkingSpaceId,
                MonthlyRate: 0m,
                StartDate: DateTime.UtcNow.Date,
                EndDate: DateTime.UtcNow.Date.AddMonths(1),
                TwoWheeler: new SlotPoolDto(20, 0, 20),
                FourWheeler: new SlotPoolDto(20, 0, 20))));

        create.Success.Should().BeFalse();
        create.Message.Should().Contain("30");
    }
}
