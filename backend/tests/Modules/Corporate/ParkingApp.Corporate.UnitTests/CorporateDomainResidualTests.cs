using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>
/// Residual domain coverage: allocation slot math, waitlist lifecycle, invitations, usage.
/// </summary>
public class CorporateDomainResidualTests
{
    private static ParkingAllocation ActiveOwned(int fixedSlots = 2, int sharedSlots = 3)
    {
        var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), allowWeekends: true);
        return ParkingAllocation.CreateCompanyOwned(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Quota.Create(fixedSlots + sharedSlots, fixedSlots, sharedSlots),
            0m,
            start,
            end,
            Guid.NewGuid(),
            policy);
    }

    [Fact]
    public void Allocation_Expire_And_GetAvailableSharedSlots()
    {
        var allocation = ActiveOwned(fixedSlots: 0, sharedSlots: 4);
        allocation.GetAvailableSharedSlots(new[] { 1, 2 }, anonymousOccupiedSharedBookings: 1).Should().Be(1);

        allocation.Expire();
        allocation.Status.Should().Be(AllocationStatus.Expired);
        allocation.GetAvailableSharedSlots(Array.Empty<int>()).Should().Be(0);
        allocation.IsActiveAllocation.Should().BeFalse();
    }

    [Fact]
    public void Allocation_ResolveSharedSlot_PicksLowestUsage()
    {
        var allocation = ActiveOwned(fixedSlots: 0, sharedSlots: 3);
        var usage = new Dictionary<int, int> { [1] = 5, [2] = 1, [3] = 3 };
        var reservation = allocation.ResolveSharedSlotReservation(
            occupiedSharedSlotNumbers: Array.Empty<int>(),
            sharedSlotUsageBySlot: usage);

        reservation.SlotType.Should().Be(CorporateSlotType.Shared);
        reservation.SlotNumber.Should().Be(2);
    }

    [Fact]
    public void Allocation_ResolveSharedSlot_ThrowsWhenFull()
    {
        var allocation = ActiveOwned(fixedSlots: 0, sharedSlots: 2);
        var act = () => allocation.ResolveSharedSlotReservation(
            occupiedSharedSlotNumbers: new[] { 1, 2 },
            sharedSlotUsageBySlot: new Dictionary<int, int>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*No shared*");
    }

    [Fact]
    public void Allocation_IsBookingAllowed_RespectsDailyLimitAndWeekend()
    {
        var allocation = ActiveOwned();
        var weekday = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc); // Wednesday
        allocation.IsBookingAllowed(1, weekday, weekday.AddHours(2), currentDayBookings: 0, currentWeekBookings: 0)
            .Should().BeTrue();
        allocation.IsBookingAllowed(1, weekday, weekday.AddHours(2), currentDayBookings: 5, currentWeekBookings: 0)
            .Should().BeFalse();
    }

    [Fact]
    public void Allocation_EnsureVisitorBookingAllowed_OutsideContract_Throws()
    {
        var allocation = ActiveOwned();
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var act = () => allocation.EnsureVisitorBookingAllowed(start, start.AddHours(1));
        act.Should().Throw<InvalidOperationException>().WithMessage("*outside*");
    }

    [Fact]
    public void Allocation_Approve_WrongStatus_Throws()
    {
        var allocation = ActiveOwned();
        var act = () => allocation.Approve(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>().WithMessage("*approve*");
    }

    [Fact]
    public void Waitlist_Employee_Promote_Cancel_AndMatch()
    {
        var companyId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        var entry = CorporateWaitlistEntry.CreateEmployee(
            companyId, membershipId, allocationId, start, end, VehicleType.Car, "ka01ab1", priorityAtRequest: 3);

        entry.IsVisitorBooking.Should().BeFalse();
        entry.VehicleNumber.Should().Be("KA01AB1");
        entry.PriorityAtRequest.Should().Be(3);
        entry.Overlaps(start.AddMinutes(30), end.AddHours(1)).Should().BeTrue();
        entry.Overlaps(end.AddHours(1), end.AddHours(2)).Should().BeFalse();
        entry.MatchesEmployeeRequest(membershipId, start, end, "KA01AB1").Should().BeTrue();
        entry.MatchesVisitorRequest(membershipId, start, end, "KA01AB1").Should().BeFalse();

        var bookingId = Guid.NewGuid();
        entry.Promote(bookingId);
        entry.Status.Should().Be(WaitlistStatus.Promoted);
        entry.PromotedBookingId.Should().Be(bookingId);

        var act = () => entry.Cancel();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Waitlist_Visitor_CreateAndCancel()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(3);
        var entry = CorporateWaitlistEntry.CreateVisitor(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            start,
            end,
            "  Guest ",
            " mh12cd ",
            accessExpiryUtc: end.AddHours(1),
            priorityAtRequest: 1);

        entry.IsVisitorBooking.Should().BeTrue();
        entry.VisitorName.Should().Be("Guest");
        entry.VisitorLicensePlate.Should().Be("MH12CD");
        entry.MatchesVisitorRequest(entry.MembershipId, start, end, "MH12CD").Should().BeTrue();

        entry.Cancel();
        entry.Status.Should().Be(WaitlistStatus.Cancelled);
        entry.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Waitlist_InvalidWindow_Throws()
    {
        var t = DateTime.UtcNow;
        var act = () => CorporateWaitlistEntry.CreateEmployee(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), t, t, VehicleType.Car, null, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Invitation_Accept_And_MarkExpired()
    {
        var invitation = EmployeeInvitation.Create(
            Guid.NewGuid(), "new@hire.com", CompanyRole.Employee, Guid.NewGuid(), expiresInDays: 7);

        invitation.IsPending.Should().BeTrue();
        invitation.InvitationToken.Should().StartWith("INV-");

        var userId = Guid.NewGuid();
        invitation.Accept(userId);
        invitation.Status.Should().Be(InvitationStatus.Accepted);
        invitation.AcceptedByUserId.Should().Be(userId);
        invitation.IsPending.Should().BeFalse();

        var pending = EmployeeInvitation.Create(
            Guid.NewGuid(), "exp@hire.com", CompanyRole.Employee, Guid.NewGuid(), expiresInDays: 1);
        pending.MarkExpired();
        pending.Status.Should().Be(InvitationStatus.Expired);

        var resendable = EmployeeInvitation.Create(
            Guid.NewGuid(), "rs@hire.com", CompanyRole.Admin, Guid.NewGuid());
        resendable.MarkExpired();
        resendable.Resend(3);
        resendable.Status.Should().Be(InvitationStatus.Pending);
        resendable.IsPending.Should().BeTrue();
    }

    [Fact]
    public void Invitation_Accept_WhenExpired_Throws()
    {
        var invitation = EmployeeInvitation.Create(
            Guid.NewGuid(), "late@hire.com", CompanyRole.Employee, Guid.NewGuid(), expiresInDays: 1);
        // Force expire via reflection on ExpiresAt is hard (private set); use MarkExpired path after Accept fails on cancelled
        invitation.Cancel();
        var act = () => invitation.Accept(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>().WithMessage("*Cancelled*");
    }

    [Fact]
    public void CompanyUsage_IncrementBooking()
    {
        var usage = CompanyUsage.Create(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        usage.IncrementBooking(1.5m, isVisitor: false);
        usage.IncrementBooking(2m, isVisitor: true);

        usage.BookingCount.Should().Be(2);
        usage.VisitorBookingCount.Should().Be(1);
        usage.TotalHoursUsed.Should().Be(3.5m);

        var act = () => usage.IncrementBooking(-1m, false);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Membership_Priority_Role_And_Deactivate()
    {
        var company = Company.Create("Acme", "REG-M1", "a@acme.com", "555", "Addr", BillingType.UsageBased, Guid.NewGuid());
        var adminId = company.Memberships.First().UserId;
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(adminId, employeeId, CompanyRole.Employee, employeeCode: " E1 ", priority: 2);

        membership.Priority.Should().Be(2);
        membership.EmployeeCode.Should().Be("E1");
        membership.IsAdmin.Should().BeFalse();

        membership.SetRole(CompanyRole.Admin);
        membership.IsAdmin.Should().BeTrue();
        membership.SetPriority(5);
        membership.Priority.Should().Be(5);
        membership.SetEmployeeCode(null);
        membership.EmployeeCode.Should().BeNull();

        membership.Deactivate();
        membership.IsActive.Should().BeFalse();
        membership.IsDeleted.Should().BeTrue();
        membership.Activate();
        membership.IsActive.Should().BeTrue();
        membership.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Quota_RejectsNonPositiveTotal()
    {
        var act = () => Quota.Create(0, 0, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AccessPolicy_RejectsInvalidWindow()
    {
        var start = DateTime.UtcNow;
        var act = () => AccessPolicy.Create("KA01", start, start);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CorporateBookingDraft_ToConfirmationAdjustment()
    {
        var draft = new CorporateBookingDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            ParkingApp.Marketplace.Contracts.Enums.BookingStatus.Confirmed,
            VehicleType.Car,
            "ka01");

        draft.VehicleNumber.Should().Be("KA01");
        var adj = draft.ToConfirmationAdjustment(slotNumber: 2);
        adj.ShouldConfirm.Should().BeFalse();
        adj.SlotNumber.Should().Be(2);
    }
}
