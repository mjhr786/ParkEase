using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using BookingStatus = ParkingApp.Marketplace.Contracts.Enums.BookingStatus;

namespace ParkingApp.Corporate.UnitTests;

public class CompanyMembershipDomainTests
{
    private static Company Create(out Guid adminId)
    {
        adminId = Guid.NewGuid();
        return Company.Create("Acme", "REG-MEM", "a@acme.com", "555", "Addr", BillingType.UsageBased, adminId);
    }

    [Fact]
    public void Invite_WhenAlreadyMemberFlag_Throws()
    {
        var company = Create(out var adminId);
        var act = () => company.InviteMember(adminId, "x@y.com", CompanyRole.Employee, emailAlreadyBelongsToMember: true);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already a member*");
    }

    [Fact]
    public void Invite_WhenPendingExists_Throws()
    {
        var company = Create(out var adminId);
        company.InviteMember(adminId, "new@hire.com", CompanyRole.Employee);
        var act = () => company.InviteMember(adminId, "NEW@hire.com", CompanyRole.Employee);
        act.Should().Throw<InvalidOperationException>().WithMessage("*pending invitation*");
    }

    [Fact]
    public void AcceptInvitation_WrongEmail_Throws()
    {
        var company = Create(out var adminId);
        var inv = company.InviteMember(adminId, "invitee@hire.com", CompanyRole.Employee);
        var act = () => company.AcceptInvitation(inv.InvitationToken, Guid.NewGuid(), "other@hire.com");
        act.Should().Throw<InvalidOperationException>().WithMessage("*different email*");
    }

    [Fact]
    public void AcceptInvitation_WhenAlreadyMember_Throws()
    {
        var company = Create(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var inv = company.InviteMember(adminId, "dup@hire.com", CompanyRole.Employee);
        var act = () => company.AcceptInvitation(inv.InvitationToken, employeeId, "dup@hire.com");
        act.Should().Throw<InvalidOperationException>().WithMessage("*already a member*");
    }

    [Fact]
    public void AcceptInvitation_Success_AddsMembership()
    {
        var company = Create(out var adminId);
        var inv = company.InviteMember(adminId, "ok@hire.com", CompanyRole.Employee);
        var userId = Guid.NewGuid();
        var membership = company.AcceptInvitation(inv.InvitationToken, userId, "ok@hire.com", "E-9", priority: 2);
        membership.UserId.Should().Be(userId);
        membership.EmployeeCode.Should().Be("E-9");
        membership.Priority.Should().Be(2);
        inv.Status.Should().Be(InvitationStatus.Accepted);
    }

    [Fact]
    public void RemoveMember_LastAdmin_Throws()
    {
        var company = Create(out var adminId);
        var adminMembership = company.Memberships.First(m => m.UserId == adminId);
        var act = () => company.RemoveMember(adminId, adminMembership.Id);
        act.Should().Throw<InvalidOperationException>().WithMessage("*last admin*");
    }

    [Fact]
    public void RemoveMember_Employee_Succeeds()
    {
        var company = Create(out var adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(adminId, employeeId, CompanyRole.Employee);
        company.RemoveMember(adminId, membership.Id);
        membership.IsActive.Should().BeFalse();
        membership.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void CancelWaitlist_OnlyOwnerOrAdmin()
    {
        var company = Create(out var adminId);
        var employeeId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        company.AddMember(adminId, otherId, CompanyRole.Employee);

        var spaceId = Guid.NewGuid();
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId, spaceId, Quota.Create(1, 0, 1), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 1,
            bookingPolicy: policy);

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var draft = new CorporateBookingDraft(
            Guid.NewGuid(), spaceId, start, end, BookingStatus.Confirmed, VehicleType.Car, "KA01");
        var fraud = CorporateFraudAssessment.None();
        var outcome = company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            draft,
            currentDayBookings: 0,
            currentWeekBookings: 0,
            occupiedSharedSlotNumbers: new[] { 1 },
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            fraud);

        outcome.IsWaitlisted.Should().BeTrue();
        outcome.WaitlistEntry.Should().NotBeNull();

        var waitlistId = outcome.WaitlistEntry!.Id;
        var actOther = () => company.CancelWaitlistEntry(otherId, waitlistId);
        actOther.Should().Throw<InvalidOperationException>().WithMessage("*admin*");

        company.CancelWaitlistEntry(employeeId, waitlistId);
        outcome.WaitlistEntry.Status.Should().Be(WaitlistStatus.Cancelled);
    }

    [Fact]
    public void ReserveVisitor_WhenNoSharedSlots_Waitlists()
    {
        var company = Create(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId, spaceId, Quota.Create(1, 0, 1), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 1,
            bookingPolicy: policy);

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var draft = new CorporateBookingDraft(
            Guid.NewGuid(), spaceId, start, end, BookingStatus.Confirmed, VehicleType.Car, "MH12");
        var outcome = company.ReserveVisitorParking(
            employeeId,
            allocation.Id,
            draft,
            "Guest",
            "MH12AB",
            end.AddHours(1),
            occupiedSharedSlotNumbers: new[] { 1 },
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            CorporateFraudAssessment.None());

        outcome.IsWaitlisted.Should().BeTrue();
        outcome.WaitlistEntry!.IsVisitorBooking.Should().BeTrue();
        outcome.WaitlistEntry.VisitorName.Should().Be("Guest");
    }

    [Fact]
    public void ReserveVisitor_WhenSlotsFree_CreatesBooking()
    {
        var company = Create(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId, spaceId, Quota.Create(2, 0, 2), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 2,
            bookingPolicy: policy);

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var bookingId = Guid.NewGuid();
        var draft = new CorporateBookingDraft(
            bookingId, spaceId, start, end, BookingStatus.Confirmed, VehicleType.Car, "MH12");
        var outcome = company.ReserveVisitorParking(
            employeeId,
            allocation.Id,
            draft,
            "Guest",
            "MH12AB",
            end.AddHours(1),
            occupiedSharedSlotNumbers: Array.Empty<int>(),
            sharedSlotUsageBySlot: new Dictionary<int, int>(),
            anonymousOccupiedSharedBookings: 0,
            CorporateFraudAssessment.None());

        outcome.IsWaitlisted.Should().BeFalse();
        outcome.Booking.Should().NotBeNull();
        outcome.Booking!.BookingId.Should().Be(bookingId);
        outcome.Booking.IsVisitorBooking.Should().BeTrue();
    }
}
