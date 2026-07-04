using FluentAssertions;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Entities.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.UnitTests.Corporate;

public class CompanyAggregateTests
{
    [Fact]
    public void Create_ShouldAddCreatorAsFirstAdmin()
    {
        var creatorId = Guid.NewGuid();

        var company = CreateCompany(creatorId);

        company.Memberships.Should().ContainSingle(m =>
            m.UserId == creatorId &&
            m.Role == CompanyRole.Admin &&
            m.IsActive);
    }

    [Fact]
    public void RemoveMember_ShouldNotAllowRemovingLastAdmin()
    {
        var creatorId = Guid.NewGuid();
        var company = CreateCompany(creatorId);
        var adminMembership = company.Memberships.Single();

        var act = () => company.RemoveMember(creatorId, adminMembership.Id);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*last admin*");
    }

    [Fact]
    public void InviteMember_ShouldRejectExistingMemberEmailWhenFlagged()
    {
        var creatorId = Guid.NewGuid();
        var company = CreateCompany(creatorId);

        var act = () => company.InviteMember(creatorId, "member@company.com", CompanyRole.Employee, emailAlreadyBelongsToMember: true);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public void InviteMember_ShouldRejectDuplicatePendingInvitation()
    {
        var creatorId = Guid.NewGuid();
        var company = CreateCompany(creatorId);
        company.Invitations.Add(EmployeeInvitation.Create(company.Id, "employee@company.com", CompanyRole.Employee, creatorId));

        var act = () => company.InviteMember(creatorId, "employee@company.com", CompanyRole.Employee);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*pending invitation*");
    }

    [Fact]
    public void AcceptInvitation_ShouldRejectUserAlreadyInCompany()
    {
        var creatorId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var company = CreateCompany(creatorId);
        company.AddMember(creatorId, employeeId, CompanyRole.Employee);
        var invitation = EmployeeInvitation.Create(company.Id, "employee@company.com", CompanyRole.Employee, creatorId);
        company.Invitations.Add(invitation);

        var act = () => company.AcceptInvitation(invitation.InvitationToken, employeeId, "employee@company.com");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public void RegisterEmployeeBooking_ShouldAssignFixedSlotAndConfirmBooking()
    {
        var creatorId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var parkingSpaceId = Guid.NewGuid();
        var company = CreateCompany(creatorId);
        var membership = company.AddMember(creatorId, employeeId, CompanyRole.Employee, priority: 3);

        var allocation = company.RequestAllocation(
            creatorId,
            parkingSpaceId,
            Quota.Create(10, 2, 8),
            1000m,
            Utc(2026, 1, 1, 0, 0),
            Utc(2026, 12, 31, 23, 59),
            parkingCapacity: 20,
            BookingPolicy.Create(2, 5, 1, new TimeSpan(7, 0, 0), new TimeSpan(22, 0, 0), false));

        company.ApproveAllocation(allocation.Id, Guid.NewGuid());
        company.AssignFixedSlot(creatorId, allocation.Id, membership.Id, 1);

        var booking = new Booking
        {
            UserId = employeeId,
            ParkingSpaceId = parkingSpaceId,
            StartDateTime = Utc(2026, 1, 7, 9, 0),
            EndDateTime = Utc(2026, 1, 7, 11, 0),
            PricingType = PricingType.Hourly,
            VehicleType = VehicleType.Car,
            Status = BookingStatus.Pending
        };

        var reservation = company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            booking,
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: Array.Empty<int>(),
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        reservation.IsWaitlisted.Should().BeFalse();
        reservation.Booking.Should().NotBeNull();
        reservation.Booking!.SlotType.Should().Be(CorporateSlotType.Fixed);
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.SlotNumber.Should().Be(1);
        company.Usages.Should().ContainSingle(u => u.AllocationId == allocation.Id);
    }

    [Fact]
    public void ReserveEmployeeParking_ShouldAutoAssignLeastUsedSharedSlot()
    {
        var creatorId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var company = CreateCompany(creatorId);
        var membership = company.AddMember(creatorId, employeeId, CompanyRole.Employee, priority: 4);

        var allocation = company.RequestAllocation(
            creatorId,
            Guid.NewGuid(),
            Quota.Create(4, 1, 3),
            1000m,
            Utc(2026, 1, 1, 0, 0),
            Utc(2026, 12, 31, 23, 59),
            parkingCapacity: 10,
            BookingPolicy.Create(2, 5, 1, new TimeSpan(7, 0, 0), new TimeSpan(22, 0, 0), false));

        company.ApproveAllocation(allocation.Id, Guid.NewGuid());

        var booking = new Booking
        {
            UserId = employeeId,
            ParkingSpaceId = allocation.ParkingSpaceId,
            StartDateTime = Utc(2026, 1, 8, 9, 0),
            EndDateTime = Utc(2026, 1, 8, 11, 0),
            PricingType = PricingType.Hourly,
            VehicleType = VehicleType.Car,
            VehicleNumber = "KA01AB1234",
            Status = BookingStatus.Pending
        };

        var reservation = company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            booking,
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: new[] { 2 },
            sharedSlotUsageBySlot: new Dictionary<int, int>
            {
                [3] = 4,
                [4] = 1
            },
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        reservation.IsWaitlisted.Should().BeFalse();
        booking.SlotNumber.Should().Be(4);
        reservation.Booking!.SlotType.Should().Be(CorporateSlotType.Shared);
        membership.Priority.Should().Be(4);
    }

    [Fact]
    public void ReserveEmployeeParking_ShouldAddMemberToWaitlistWhenSharedSlotsAreFull()
    {
        var creatorId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var company = CreateCompany(creatorId);
        company.AddMember(creatorId, employeeId, CompanyRole.Employee, priority: 2);

        var allocation = CreateActiveSharedAllocation(company, creatorId, totalSlots: 2, fixedSlots: 1, sharedSlots: 1);

        var booking = new Booking
        {
            UserId = employeeId,
            ParkingSpaceId = allocation.ParkingSpaceId,
            StartDateTime = Utc(2026, 1, 9, 9, 0),
            EndDateTime = Utc(2026, 1, 9, 10, 0),
            PricingType = PricingType.Hourly,
            VehicleType = VehicleType.Car,
            VehicleNumber = "DL01AA1111",
            Status = BookingStatus.Pending
        };

        var reservation = company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            booking,
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: new[] { 2 },
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        reservation.IsWaitlisted.Should().BeTrue();
        reservation.WaitlistEntry.Should().NotBeNull();
        company.GetWaitlistPosition(reservation.WaitlistEntry!.Id).Should().Be(1);
        company.WaitlistEntries.Should().ContainSingle(w => w.Status == WaitlistStatus.Pending);
    }

    [Fact]
    public void ReserveEmployeeParking_ShouldPromoteWaitlistHeadWhenSharedSlotOpens()
    {
        var creatorId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var company = CreateCompany(creatorId);
        company.AddMember(creatorId, employeeId, CompanyRole.Employee, priority: 2);

        var allocation = CreateActiveSharedAllocation(company, creatorId, totalSlots: 2, fixedSlots: 1, sharedSlots: 1);
        var start = Utc(2026, 1, 9, 9, 0);
        var end = Utc(2026, 1, 9, 10, 0);

        var waitlisted = company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            CreatePendingBooking(employeeId, allocation.ParkingSpaceId, "DL01AA1111", start, end),
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: new[] { 2 },
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        var promoted = company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            CreatePendingBooking(employeeId, allocation.ParkingSpaceId, "DL01AA1111", start, end),
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: Array.Empty<int>(),
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        promoted.IsWaitlisted.Should().BeFalse();
        promoted.Booking.Should().NotBeNull();
        waitlisted.WaitlistEntry!.Status.Should().Be(WaitlistStatus.Promoted);
        waitlisted.WaitlistEntry.PromotedBookingId.Should().Be(promoted.Booking!.BookingId);
    }

    [Fact]
    public void CancelWaitlistEntry_ShouldCancelPendingEntryForRequester()
    {
        var creatorId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var company = CreateCompany(creatorId);
        company.AddMember(creatorId, employeeId, CompanyRole.Employee, priority: 2);
        var allocation = CreateActiveSharedAllocation(company, creatorId, totalSlots: 2, fixedSlots: 1, sharedSlots: 1);

        var reservation = company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            CreatePendingBooking(employeeId, allocation.ParkingSpaceId, "DL01AA1111", Utc(2026, 1, 9, 9, 0), Utc(2026, 1, 9, 10, 0)),
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: new[] { 2 },
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        company.CancelWaitlistEntry(employeeId, reservation.WaitlistEntry!.Id);

        reservation.WaitlistEntry.Status.Should().Be(WaitlistStatus.Cancelled);
        reservation.WaitlistEntry.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void ReserveEmployeeParking_ShouldPrioritizeVipWaitlistOverEarlierLowerPriorityRequest()
    {
        var creatorId = Guid.NewGuid();
        var lowPriorityUserId = Guid.NewGuid();
        var vipUserId = Guid.NewGuid();
        var company = CreateCompany(creatorId);
        company.AddMember(creatorId, lowPriorityUserId, CompanyRole.Employee, priority: 2);
        company.AddMember(creatorId, vipUserId, CompanyRole.Employee, priority: 10);

        var allocation = CreateActiveSharedAllocation(company, creatorId, totalSlots: 2, fixedSlots: 1, sharedSlots: 1);

        var lowPriorityReservation = company.ReserveEmployeeParking(
            lowPriorityUserId,
            allocation.Id,
            CreatePendingBooking(lowPriorityUserId, allocation.ParkingSpaceId, "MH01LOW123", Utc(2026, 1, 12, 9, 0), Utc(2026, 1, 12, 10, 0)),
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: new[] { 2 },
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        var vipReservation = company.ReserveEmployeeParking(
            vipUserId,
            allocation.Id,
            CreatePendingBooking(vipUserId, allocation.ParkingSpaceId, "MH01VIP123", Utc(2026, 1, 12, 9, 0), Utc(2026, 1, 12, 10, 0)),
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: new[] { 2 },
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        lowPriorityReservation.IsWaitlisted.Should().BeTrue();
        vipReservation.IsWaitlisted.Should().BeTrue();
        company.GetWaitlistPosition(vipReservation.WaitlistEntry!.Id).Should().Be(1);
        company.GetWaitlistPosition(lowPriorityReservation.WaitlistEntry!.Id).Should().Be(2);

        var lowPriorityRetry = company.ReserveEmployeeParking(
            lowPriorityUserId,
            allocation.Id,
            CreatePendingBooking(lowPriorityUserId, allocation.ParkingSpaceId, "MH01LOW123", Utc(2026, 1, 12, 9, 0), Utc(2026, 1, 12, 10, 0)),
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: Array.Empty<int>(),
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: CorporateFraudAssessment.None());

        lowPriorityRetry.IsWaitlisted.Should().BeTrue();
        lowPriorityRetry.Booking.Should().BeNull();
    }

    [Fact]
    public void ReserveEmployeeParking_ShouldRejectBlockedFraudAssessment()
    {
        var creatorId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var company = CreateCompany(creatorId);
        company.AddMember(creatorId, employeeId, CompanyRole.Employee, priority: 3);
        var allocation = CreateActiveSharedAllocation(company, creatorId, totalSlots: 2, fixedSlots: 1, sharedSlots: 1);

        var booking = CreatePendingBooking(
            employeeId,
            allocation.ParkingSpaceId,
            "KA01FRAUD1",
            Utc(2026, 1, 12, 9, 0),
            Utc(2026, 1, 12, 10, 0));

        var fraudAssessment = company.AssessFraudRisk(
            employeeId,
            booking.StartDateTime,
            booking.EndDateTime,
            hasOverlappingMemberBooking: true,
            hasOverlappingVehicleBooking: false,
            recentBookingCreations: 1);

        var act = () => company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            booking,
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: Array.Empty<int>(),
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraudAssessment: fraudAssessment);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*duplicate booking*");
        fraudAssessment.RiskLevel.Should().Be(CorporateFraudRiskLevel.High);
    }

    [Fact]
    public void CreateOwnedParkingAllocation_ShouldActivateWithoutVendorApproval()
    {
        var creatorId = Guid.NewGuid();
        var parkingSpaceId = Guid.NewGuid();
        var company = CreateCompany(creatorId);

        var allocation = company.CreateOwnedParkingAllocation(
            creatorId,
            parkingSpaceId,
            Quota.Create(6, 2, 4),
            0m,
            Utc(2026, 1, 1, 0, 0),
            Utc(2026, 12, 31, 23, 59),
            parkingCapacity: 10,
            BookingPolicy.Create(2, 5, 1, new TimeSpan(7, 0, 0), new TimeSpan(22, 0, 0), false));

        allocation.Status.Should().Be(AllocationStatus.Active);
        allocation.SourceType.Should().Be(ParkingAllocationSource.CompanyOwned);
        allocation.ApprovedByUserId.Should().Be(creatorId);
        allocation.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public void CreateOwnedParkingAllocation_ShouldRejectQuotaBeyondOwnedCapacity()
    {
        var creatorId = Guid.NewGuid();
        var company = CreateCompany(creatorId);

        var act = () => company.CreateOwnedParkingAllocation(
            creatorId,
            Guid.NewGuid(),
            Quota.Create(11, 2, 9),
            0m,
            Utc(2026, 1, 1, 0, 0),
            Utc(2026, 12, 31, 23, 59),
            parkingCapacity: 10,
            BookingPolicy.Create(2, 5, 1, new TimeSpan(7, 0, 0), new TimeSpan(22, 0, 0), false));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Cannot allocate more than 10*");
    }

    [Fact]
    public void EnsureEmployeeBookingAllowed_ShouldEnforceDailyLimit()
    {
        var allocation = ParkingAllocation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Quota.Create(6, 1, 5),
            500m,
            Utc(2026, 1, 1, 0, 0),
            Utc(2026, 12, 31, 23, 59),
            BookingPolicy.Create(1, 5, 1, new TimeSpan(7, 0, 0), new TimeSpan(22, 0, 0), false));

        allocation.Approve(Guid.NewGuid());

        var act = () => allocation.EnsureEmployeeBookingAllowed(
            memberPriority: 2,
            bookingStart: Utc(2026, 1, 8, 9, 0),
            bookingEnd: Utc(2026, 1, 8, 10, 0),
            currentDayBookings: 1,
            currentWeekBookings: 1);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Daily booking limit*");
    }

    private static Company CreateCompany(Guid creatorId)
    {
        return Company.Create(
            "Acme Parking",
            "REG-001",
            "owner@company.com",
            "9999999999",
            "Corporate Address",
            BillingType.UsageBased,
            creatorId);
    }

    private static ParkingAllocation CreateActiveSharedAllocation(Company company, Guid creatorId, int totalSlots, int fixedSlots, int sharedSlots)
    {
        var allocation = company.RequestAllocation(
            creatorId,
            Guid.NewGuid(),
            Quota.Create(totalSlots, fixedSlots, sharedSlots),
            800m,
            Utc(2026, 1, 1, 0, 0),
            Utc(2026, 12, 31, 23, 59),
            parkingCapacity: 10,
            BookingPolicy.Create(2, 5, 1, new TimeSpan(7, 0, 0), new TimeSpan(22, 0, 0), false));

        company.ApproveAllocation(allocation.Id, Guid.NewGuid());
        return allocation;
    }

    private static Booking CreatePendingBooking(Guid userId, Guid parkingSpaceId, string vehicleNumber, DateTime startUtc, DateTime endUtc)
    {
        return new Booking
        {
            UserId = userId,
            ParkingSpaceId = parkingSpaceId,
            StartDateTime = startUtc,
            EndDateTime = endUtc,
            PricingType = PricingType.Hourly,
            VehicleType = VehicleType.Car,
            VehicleNumber = vehicleNumber,
            Status = BookingStatus.Pending
        };
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute)
    {
        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }
}
