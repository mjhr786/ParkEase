using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Marketplace.Contracts.Enums;
using Xunit;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>
/// Dual 2W/4W class pool domain matrix (architecture plan §8 + M11/M16).
/// </summary>
public class VehicleClassPoolTests
{
    private static Company CreateCompany(out Guid adminId)
    {
        adminId = Guid.NewGuid();
        return Company.Create("Acme", $"REG-{Guid.NewGuid():N}"[..12], "a@b.com", "1", "addr",
            BillingType.UsageBased, adminId);
    }

    private static BookingPolicy OpenPolicy() =>
        BookingPolicy.Create(10, 40, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), allowWeekends: true);

    private static CorporateBookingDraft Draft(
        Guid spaceId, DateTime start, DateTime end, VehicleType type, string plate) =>
        new(Guid.NewGuid(), spaceId, start, end, BookingStatus.Confirmed, type, plate);

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void VehicleClassMapper_MapsMotorcycleToTwoWheeler()
    {
        VehicleClassMapper.ToVehicleClass(VehicleType.Motorcycle).Should().Be(VehicleClass.TwoWheeler);
        VehicleClassMapper.ToVehicleClass(VehicleType.Car).Should().Be(VehicleClass.FourWheeler);
        VehicleClassMapper.ToVehicleClass(VehicleType.SUV).Should().Be(VehicleClass.FourWheeler);
        VehicleClassMapper.ToVehicleClass(VehicleType.Electric).Should().Be(VehicleClass.FourWheeler);
        VehicleClassMapper.ToVehicleClass(VehicleType.Truck).Should().Be(VehicleClass.FourWheeler);
        VehicleClassMapper.ToVehicleClass(VehicleType.Van).Should().Be(VehicleClass.FourWheeler);
    }

    [Theory]
    [Trait("Feature", "VehicleClassPools")]
    [InlineData(null, VehicleClass.FourWheeler, true)]
    [InlineData(1, VehicleClass.TwoWheeler, true)]   // Motorcycle
    [InlineData(1, VehicleClass.FourWheeler, false)]
    [InlineData(0, VehicleClass.FourWheeler, true)]  // Car
    [InlineData(0, VehicleClass.TwoWheeler, false)]
    [InlineData(2, VehicleClass.FourWheeler, true)]  // SUV
    public void BookingMatchesClass_MirrorsOccupancySqlSemantics(
        int? vehicleTypeValue, VehicleClass vehicleClass, bool expected)
    {
        VehicleClassMapper.BookingMatchesClass(vehicleTypeValue, vehicleClass).Should().Be(expected);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void Quota_CreatePool_AllowsZero_AndCombineRequiresCapacity()
    {
        var empty = Quota.CreatePool(0, 0, 0);
        empty.IsEmpty.Should().BeTrue();

        var four = Quota.Create(10, 2, 8);
        var combined = Quota.Combine(empty, four);
        combined.TotalSlots.Should().Be(10);
        combined.FixedSlots.Should().Be(2);
        combined.SharedSlots.Should().Be(8);

        var bothEmpty = () => Quota.Combine(Quota.None, Quota.None);
        bothEmpty.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void CreateOwned_DualPools_WithinCapacity_Succeeds()
    {
        var company = CreateCompany(out var adminId);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            Guid.NewGuid(),
            twoWheelerQuota: Quota.CreatePool(20, 0, 20),
            fourWheelerQuota: Quota.CreatePool(10, 0, 10),
            monthlyRate: 0m,
            startDate: DateTime.UtcNow.Date,
            endDate: DateTime.UtcNow.Date.AddYears(1),
            parkingCapacity: 30,
            bookingPolicy: OpenPolicy());

        allocation.TwoWheelerQuota.TotalSlots.Should().Be(20);
        allocation.FourWheelerQuota.TotalSlots.Should().Be(10);
        allocation.Quota.TotalSlots.Should().Be(30);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void CreateOwned_DualPools_ExceedCapacity_Throws()
    {
        var company = CreateCompany(out var adminId);
        var act = () => company.CreateOwnedParkingAllocation(
            adminId,
            Guid.NewGuid(),
            twoWheelerQuota: Quota.CreatePool(20, 0, 20),
            fourWheelerQuota: Quota.CreatePool(20, 0, 20),
            monthlyRate: 0m,
            startDate: DateTime.UtcNow.Date,
            endDate: DateTime.UtcNow.Date.AddYears(1),
            parkingCapacity: 30);

        act.Should().Throw<InvalidOperationException>().WithMessage("*30*");
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void DualPoolAllocation_MotorcycleDoesNotUseFourWheelerShared()
    {
        var company = CreateCompany(out var adminId);
        var spaceId = Guid.NewGuid();

        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            spaceId,
            twoWheelerQuota: Quota.CreatePool(2, 0, 2),
            fourWheelerQuota: Quota.CreatePool(5, 0, 5),
            monthlyRate: 0m,
            startDate: DateTime.UtcNow.Date,
            endDate: DateTime.UtcNow.Date.AddYears(1),
            parkingCapacity: 20,
            bookingPolicy: OpenPolicy());

        allocation.TwoWheelerQuota.TotalSlots.Should().Be(2);
        allocation.FourWheelerQuota.TotalSlots.Should().Be(5);
        allocation.Quota.TotalSlots.Should().Be(7);

        var occupied2W = new[] { 1, 2 };
        allocation.GetAvailableSharedSlots(VehicleClass.TwoWheeler, occupied2W).Should().Be(0);
        allocation.GetAvailableSharedSlots(VehicleClass.FourWheeler, Array.Empty<int>()).Should().Be(5);

        allocation.EnsureClassOffered(VehicleClass.TwoWheeler);

        var zeroBoth = () => company.RequestAllocation(
            adminId,
            Guid.NewGuid(),
            Quota.CreatePool(0, 0, 0),
            Quota.CreatePool(0, 0, 0),
            0m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddMonths(1),
            parkingCapacity: 10);
        zeroBoth.Should().Throw<InvalidOperationException>().WithMessage("*at least one*");
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void GetAvailableSharedSlots_FourWheelerFull_TwoWheelerFree()
    {
        var company = CreateCompany(out var adminId);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            Guid.NewGuid(),
            twoWheelerQuota: Quota.CreatePool(3, 0, 3),
            fourWheelerQuota: Quota.CreatePool(2, 0, 2),
            monthlyRate: 0m,
            startDate: DateTime.UtcNow.Date,
            endDate: DateTime.UtcNow.Date.AddYears(1),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());

        allocation.GetAvailableSharedSlots(VehicleClass.FourWheeler, new[] { 1, 2 }).Should().Be(0);
        allocation.GetAvailableSharedSlots(VehicleClass.TwoWheeler, Array.Empty<int>()).Should().Be(3);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void EnsureClassOffered_WhenTwoWheelerZero_Throws()
    {
        var company = CreateCompany(out var adminId);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            Guid.NewGuid(),
            twoWheelerQuota: Quota.None,
            fourWheelerQuota: Quota.CreatePool(5, 0, 5),
            monthlyRate: 0m,
            startDate: DateTime.UtcNow.Date,
            endDate: DateTime.UtcNow.Date.AddYears(1),
            parkingCapacity: 5,
            bookingPolicy: OpenPolicy());

        var act = () => allocation.EnsureClassOffered(VehicleClass.TwoWheeler);
        act.Should().Throw<InvalidOperationException>().WithMessage("*2-wheeler*");
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void FixedSlots_AreScopedPerVehicleClass()
    {
        var company = CreateCompany(out var adminId);
        var member = company.AddMember(adminId, Guid.NewGuid(), CompanyRole.Employee);
        var spaceId = Guid.NewGuid();

        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            spaceId,
            twoWheelerQuota: Quota.CreatePool(3, 1, 2),
            fourWheelerQuota: Quota.CreatePool(3, 1, 2),
            monthlyRate: 0m,
            startDate: DateTime.UtcNow.Date,
            endDate: DateTime.UtcNow.Date.AddYears(1),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());

        company.AssignFixedSlot(adminId, allocation.Id, member.Id, VehicleClass.TwoWheeler, 1);
        company.AssignFixedSlot(adminId, allocation.Id, member.Id, VehicleClass.FourWheeler, 1);

        allocation.HasFixedSlotAssignment(member.Id, VehicleClass.TwoWheeler).Should().BeTrue();
        allocation.HasFixedSlotAssignment(member.Id, VehicleClass.FourWheeler).Should().BeTrue();

        var reservation2W = allocation.ResolveSlotReservation(
            member.Id, VehicleClass.TwoWheeler, Array.Empty<int>(), new Dictionary<int, int>());
        reservation2W.SlotNumber.Should().Be(1);
        reservation2W.SlotType.Should().Be(CorporateSlotType.Fixed);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void AssignFixed_TwoWheelerSlotOutOfRange_Throws()
    {
        var company = CreateCompany(out var adminId);
        var member = company.AddMember(adminId, Guid.NewGuid(), CompanyRole.Employee);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            Guid.NewGuid(),
            twoWheelerQuota: Quota.CreatePool(3, 1, 2),
            fourWheelerQuota: Quota.CreatePool(3, 1, 2),
            monthlyRate: 0m,
            startDate: DateTime.UtcNow.Date,
            endDate: DateTime.UtcNow.Date.AddYears(1),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());

        var act = () => company.AssignFixedSlot(adminId, allocation.Id, member.Id, VehicleClass.TwoWheeler, 2);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void ResolveSlotReservation_UsesFixedOnlyForMatchingClass()
    {
        var company = CreateCompany(out var adminId);
        var member = company.AddMember(adminId, Guid.NewGuid(), CompanyRole.Employee);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            Guid.NewGuid(),
            twoWheelerQuota: Quota.CreatePool(2, 0, 2),
            fourWheelerQuota: Quota.CreatePool(3, 1, 2),
            monthlyRate: 0m,
            startDate: DateTime.UtcNow.Date,
            endDate: DateTime.UtcNow.Date.AddYears(1),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());

        company.AssignFixedSlot(adminId, allocation.Id, member.Id, VehicleClass.FourWheeler, 1);

        // Motorcycle must not take 4W fixed bay
        var bike = allocation.ResolveSlotReservation(
            member.Id, VehicleClass.TwoWheeler, Array.Empty<int>(), new Dictionary<int, int>());
        bike.SlotType.Should().Be(CorporateSlotType.Shared);

        var car = allocation.ResolveSlotReservation(
            member.Id, VehicleClass.FourWheeler, Array.Empty<int>(), new Dictionary<int, int>());
        car.SlotType.Should().Be(CorporateSlotType.Fixed);
        car.SlotNumber.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void ReserveEmployee_Motorcycle_WhenTwoWheelerFull_Waitlists_DespiteFourWheelerFree()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            spaceId,
            twoWheelerQuota: Quota.CreatePool(1, 0, 1),
            fourWheelerQuota: Quota.CreatePool(5, 0, 5),
            monthlyRate: 0m,
            startDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        // 2W shared slot 1 occupied; 4W all free
        var outcome = company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            Draft(spaceId, start, end, VehicleType.Motorcycle, "KA01BIKE"),
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: new[] { 1 },
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        outcome.IsWaitlisted.Should().BeTrue();
        outcome.Booking.Should().BeNull();
        company.CorporateBookings.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void ReserveEmployee_Car_WhenFourWheelerFull_Waitlists_DespiteTwoWheelerFree()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            spaceId,
            twoWheelerQuota: Quota.CreatePool(5, 0, 5),
            fourWheelerQuota: Quota.CreatePool(1, 0, 1),
            monthlyRate: 0m,
            startDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        var outcome = company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            Draft(spaceId, start, end, VehicleType.Car, "KA01CAR1"),
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: new[] { 1 },
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        outcome.IsWaitlisted.Should().BeTrue();
        outcome.Booking.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void ReserveEmployee_Motorcycle_WhenTwoWheelerPoolZero_Throws()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            spaceId,
            twoWheelerQuota: Quota.None,
            fourWheelerQuota: Quota.CreatePool(5, 0, 5),
            monthlyRate: 0m,
            startDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 5,
            bookingPolicy: OpenPolicy());

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        var act = () => company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            Draft(spaceId, start, end, VehicleType.Motorcycle, "KA01BIKE"),
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: Array.Empty<int>(),
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        act.Should().Throw<InvalidOperationException>().WithMessage("*2-wheeler*");
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void ReserveEmployee_WithTwoWheelerFixed_UsesFixedBay()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        var member = company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            spaceId,
            twoWheelerQuota: Quota.CreatePool(2, 1, 1),
            fourWheelerQuota: Quota.CreatePool(2, 0, 2),
            monthlyRate: 0m,
            startDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());

        company.AssignFixedSlot(adminId, allocation.Id, member.Id, VehicleClass.TwoWheeler, 1);

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        // Even if shared 2W is "full", fixed path should book
        var outcome = company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            Draft(spaceId, start, end, VehicleType.Motorcycle, "KA01BIKE"),
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: new[] { 2 },
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        outcome.IsWaitlisted.Should().BeFalse();
        outcome.Booking.Should().NotBeNull();
        outcome.Booking!.SlotType.Should().Be(CorporateSlotType.Fixed);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void WaitlistHead_IsScopedPerVehicleClass()
    {
        var company = CreateCompany(out var adminId);
        var bikeUser = Guid.NewGuid();
        var carUser = Guid.NewGuid();
        company.AddMember(adminId, bikeUser, CompanyRole.Employee);
        company.AddMember(adminId, carUser, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            spaceId,
            twoWheelerQuota: Quota.CreatePool(1, 0, 1),
            fourWheelerQuota: Quota.CreatePool(1, 0, 1),
            monthlyRate: 0m,
            startDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        // Fill both pools → both waitlisted
        var bikeWait = company.ReserveEmployeeParking(
            bikeUser, allocation.Id,
            Draft(spaceId, start, end, VehicleType.Motorcycle, "BIKE1"),
            0, 0, new[] { 1 }, new Dictionary<int, int>(), 0, CorporateFraudAssessment.None());
        var carWait = company.ReserveEmployeeParking(
            carUser, allocation.Id,
            Draft(spaceId, start, end, VehicleType.Car, "CAR1"),
            0, 0, new[] { 1 }, new Dictionary<int, int>(), 0, CorporateFraudAssessment.None());

        bikeWait.IsWaitlisted.Should().BeTrue();
        carWait.IsWaitlisted.Should().BeTrue();

        // Free 4W capacity: car books; bike remains waitlisted (class isolation)
        var carBook = company.ReserveEmployeeParking(
            carUser, allocation.Id,
            Draft(spaceId, start, end, VehicleType.Car, "CAR1"),
            0, 0, Array.Empty<int>(), new Dictionary<int, int>(), 0, CorporateFraudAssessment.None());
        carBook.IsWaitlisted.Should().BeFalse();
        carBook.Booking.Should().NotBeNull();

        var bikeStillBlocked = company.ReserveEmployeeParking(
            bikeUser, allocation.Id,
            Draft(spaceId, start, end, VehicleType.Motorcycle, "BIKE1"),
            0, 0, new[] { 1 }, new Dictionary<int, int>(), 0, CorporateFraudAssessment.None());
        // Still blocked while 2W occupancy full; waitlist entry may promote or re-match
        bikeStillBlocked.IsWaitlisted.Should().BeTrue();
    }
}
