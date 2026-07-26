using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using BookingStatus = ParkingApp.Marketplace.Contracts.Enums.BookingStatus;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>
/// Wave 17 domain residual: Company guard paths, invitation/waitlist edges,
/// allocation IsBookingAllowed ArgumentException, invoice max lines/currency default.
/// </summary>
public class DomainResidualWave17Tests
{
    private static Company CreateCompany(out Guid adminId)
    {
        adminId = Guid.NewGuid();
        return Company.Create("Wave17 Co", "REG-W17", "w17@acme.com", "555", "Addr", BillingType.UsageBased, adminId);
    }

    private static BookingPolicy DefaultPolicy(bool weekends = true) =>
        BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), weekends);

    private static ParkingAllocation ApproveOwned(Company company, Guid adminId, Guid spaceId, int fixedSlots = 0, int sharedSlots = 2)
    {
        // Company-owned allocations are Active immediately (no vendor approval).
        return company.CreateOwnedParkingAllocation(
            adminId,
            spaceId,
            Quota.Create(fixedSlots + sharedSlots, fixedSlots, sharedSlots),
            0m,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 50,
            DefaultPolicy());
    }

    private static CorporateBookingDraft Draft(
        Guid spaceId,
        DateTime start,
        DateTime end,
        BookingStatus status = BookingStatus.Pending,
        Guid? bookingId = null) =>
        new(bookingId ?? Guid.NewGuid(), spaceId, start, end, status, VehicleType.Car, "KA01AB1");

    // ── Company: inactive / missing resources / null guards ─────────────────

    [Fact]
    public void Company_Deactivated_BlocksAdminActions()
    {
        var company = CreateCompany(out var adminId);
        company.Deactivate();

        var invite = () => company.InviteMember(adminId, "x@y.com", CompanyRole.Employee);
        invite.Should().Throw<InvalidOperationException>().WithMessage("*inactive*");

        company.Activate();
        company.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Company_UpdateMember_Missing_Throws()
    {
        var company = CreateCompany(out var adminId);
        var act = () => company.UpdateMember(adminId, Guid.NewGuid(), role: CompanyRole.Employee);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Membership not found*");
    }

    [Fact]
    public void Company_CancelAndResendInvitation_Missing_Throws()
    {
        var company = CreateCompany(out var adminId);
        var cancel = () => company.CancelInvitation(adminId, Guid.NewGuid());
        cancel.Should().Throw<InvalidOperationException>().WithMessage("*Invitation not found*");

        var resend = () => company.ResendInvitation(adminId, Guid.NewGuid());
        resend.Should().Throw<InvalidOperationException>().WithMessage("*Invitation not found*");
    }

    [Fact]
    public void Company_CancelWaitlist_Missing_Throws()
    {
        var company = CreateCompany(out var adminId);
        var act = () => company.CancelWaitlistEntry(adminId, Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>().WithMessage("*Waitlist entry not found*");
    }

    [Fact]
    public void Company_RequestAndOwnedAllocation_NullQuota_Throws()
    {
        var company = CreateCompany(out var adminId);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var request = () => company.RequestAllocation(adminId, Guid.NewGuid(), null!, 100m, start, end, 10);
        request.Should().Throw<ArgumentNullException>().WithParameterName("quota");

        var owned = () => company.CreateOwnedParkingAllocation(adminId, Guid.NewGuid(), null!, 0m, start, end, 10);
        owned.Should().Throw<ArgumentNullException>().WithParameterName("quota");
    }

    [Fact]
    public void Company_AllocationWindow_EndBeforeStart_Throws()
    {
        var company = CreateCompany(out var adminId);
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var act = () => company.RequestAllocation(
            adminId, Guid.NewGuid(), Quota.Create(2, 0, 2), 100m, start, end, parkingCapacity: 10);
        act.Should().Throw<ArgumentException>().WithMessage("*End date*");
    }

    [Fact]
    public void Company_Allocation_LocalDateKind_Normalizes()
    {
        var company = CreateCompany(out var adminId);
        var startLocal = DateTime.SpecifyKind(new DateTime(2026, 3, 1, 0, 0, 0), DateTimeKind.Local);
        var endLocal = startLocal.AddMonths(6);

        var allocation = company.RequestAllocation(
            adminId, Guid.NewGuid(), Quota.Create(1, 0, 1), 50m, startLocal, endLocal, parkingCapacity: 5);

        allocation.StartDate.Kind.Should().Be(DateTimeKind.Utc);
        allocation.EndDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Company_RemoveMember_Missing_Throws()
    {
        var company = CreateCompany(out var adminId);
        var act = () => company.RemoveMember(adminId, Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>().WithMessage("*Membership not found*");
    }

    [Fact]
    public void Company_AddMember_EmptyUserId_Throws()
    {
        var company = CreateCompany(out var adminId);
        var act = () => company.AddMember(adminId, Guid.Empty, CompanyRole.Employee);
        act.Should().Throw<ArgumentException>().WithMessage("*User ID*");
    }

    [Fact]
    public void Company_AddMember_Duplicate_Throws()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var act = () => company.AddMember(adminId, employeeId, CompanyRole.Employee);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already a member*");
    }

    [Fact]
    public void Company_NonAdmin_CannotInvite()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var act = () => company.InviteMember(employeeId, "new@hire.com", CompanyRole.Employee);
        act.Should().Throw<InvalidOperationException>().WithMessage("*admins*");
    }

    [Fact]
    public void Company_InactiveMember_CannotReserve()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(adminId, employeeId, CompanyRole.Employee);
        membership.Deactivate();

        var spaceId = Guid.NewGuid();
        var allocation = ApproveOwned(company, adminId, spaceId);
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);

        var act = () => company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            Draft(spaceId, start, start.AddHours(2)),
            0, 0,
            Array.Empty<int>(),
            new Dictionary<int, int>(),
            0,
            CorporateFraudAssessment.None());

        act.Should().Throw<InvalidOperationException>().WithMessage("*active member*");
    }

    [Fact]
    public void Company_AssignFixedSlot_MissingAllocation_Throws()
    {
        var company = CreateCompany(out var adminId);
        var membership = company.Memberships.First(m => m.UserId == adminId);
        var act = () => company.AssignFixedSlot(adminId, Guid.NewGuid(), membership.Id, 1);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Allocation not found*");
    }

    [Fact]
    public void Company_AssignFixedSlot_PendingAllocation_Throws()
    {
        var company = CreateCompany(out var adminId);
        var membership = company.AddMember(adminId, Guid.NewGuid(), CompanyRole.Employee);
        // Vendor lease RequestAllocation stays PendingApproval until approved.
        var allocation = company.RequestAllocation(
            adminId,
            Guid.NewGuid(),
            Quota.Create(2, 2, 0),
            100m,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 10,
            DefaultPolicy());

        allocation.IsActiveAllocation.Should().BeFalse();
        var act = () => company.AssignFixedSlot(adminId, allocation.Id, membership.Id, 1);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Active allocation*");
    }

    [Fact]
    public void Company_AssignFixedSlot_DeactivatedMember_Throws()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(adminId, employeeId, CompanyRole.Employee);
        // Deactivate soft-deletes membership → RequireMembershipById reports not found.
        membership.Deactivate();
        var allocation = ApproveOwned(company, adminId, Guid.NewGuid(), fixedSlots: 2, sharedSlots: 0);

        var act = () => company.AssignFixedSlot(adminId, allocation.Id, membership.Id, 1);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Membership not found*");
    }

    [Fact]
    public void Company_Invite_EmptyEmail_Throws()
    {
        var company = CreateCompany(out var adminId);
        var act = () => company.InviteMember(adminId, "  ", CompanyRole.Employee);
        act.Should().Throw<ArgumentException>().WithMessage("*Email*");
    }

    [Fact]
    public void Company_Reserve_MismatchedParkingSpace_Throws()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = ApproveOwned(company, adminId, spaceId);
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);

        var act = () => company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            Draft(Guid.NewGuid(), start, start.AddHours(2)),
            0, 0,
            Array.Empty<int>(),
            new Dictionary<int, int>(),
            0,
            CorporateFraudAssessment.None());

        act.Should().Throw<InvalidOperationException>().WithMessage("*parking space does not match*");
    }

    [Fact]
    public void Company_Reserve_EndBeforeStart_Throws()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = ApproveOwned(company, adminId, spaceId);
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);

        var act = () => company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            Draft(spaceId, start, start.AddHours(-1)),
            0, 0,
            Array.Empty<int>(),
            new Dictionary<int, int>(),
            0,
            CorporateFraudAssessment.None());

        act.Should().Throw<ArgumentException>().WithMessage("*end time*");
    }

    [Fact]
    public void Company_Reserve_InvalidBookingStatus_Throws()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = ApproveOwned(company, adminId, spaceId, fixedSlots: 1, sharedSlots: 0);
        company.AssignFixedSlot(adminId, allocation.Id, membership.Id, 1);
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);

        var act = () => company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            Draft(spaceId, start, start.AddHours(2), BookingStatus.Cancelled),
            0, 0,
            Array.Empty<int>(),
            new Dictionary<int, int>(),
            0,
            CorporateFraudAssessment.None());

        act.Should().Throw<InvalidOperationException>().WithMessage("*confirmed before registration*");
    }

    [Fact]
    public void Company_Reserve_NullFraudAssessment_Throws()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = ApproveOwned(company, adminId, spaceId, fixedSlots: 1, sharedSlots: 0);
        company.AssignFixedSlot(adminId, allocation.Id, membership.Id, 1);
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);

        var act = () => company.ReserveEmployeeParking(
            employeeId,
            allocation.Id,
            Draft(spaceId, start, start.AddHours(2)),
            0, 0,
            Array.Empty<int>(),
            new Dictionary<int, int>(),
            0,
            fraudAssessment: null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("fraudAssessment");
    }

    // ── EmployeeInvitation residual ─────────────────────────────────────────

    [Fact]
    public void Invitation_Accept_WhenExpired_Throws()
    {
        var inv = EmployeeInvitation.Create(Guid.NewGuid(), "a@b.com", CompanyRole.Employee, Guid.NewGuid());
        // Force past expiry while still Pending (private setter).
        typeof(EmployeeInvitation)
            .GetProperty(nameof(EmployeeInvitation.ExpiresAt))!
            .SetValue(inv, DateTime.UtcNow.AddMinutes(-5));

        inv.IsExpired.Should().BeTrue();
        var act = () => inv.Accept(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>().WithMessage("*expired*");
        inv.Status.Should().Be(InvitationStatus.Expired);
    }

    [Fact]
    public void Invitation_Accept_WhenCancelled_Throws()
    {
        var pending = EmployeeInvitation.Create(Guid.NewGuid(), "b@c.com", CompanyRole.Employee, Guid.NewGuid());
        pending.Cancel();
        var cancelAccept = () => pending.Accept(Guid.NewGuid());
        cancelAccept.Should().Throw<InvalidOperationException>().WithMessage("*status*");
    }

    [Fact]
    public void Invitation_Accept_EmptyUser_Throws()
    {
        var inv = EmployeeInvitation.Create(Guid.NewGuid(), "a@b.com", CompanyRole.Employee, Guid.NewGuid());
        var act = () => inv.Accept(Guid.Empty);
        act.Should().Throw<ArgumentException>().WithMessage("*User ID*");
    }

    [Fact]
    public void Invitation_Resend_InvalidExpiryAndAccepted_Throws()
    {
        var inv = EmployeeInvitation.Create(Guid.NewGuid(), "a@b.com", CompanyRole.Employee, Guid.NewGuid());
        var badDays = () => inv.Resend(0);
        badDays.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("expiresInDays");

        inv.Accept(Guid.NewGuid());
        var resendAccepted = () => inv.Resend(7);
        resendAccepted.Should().Throw<InvalidOperationException>().WithMessage("*resend*");
    }

    [Fact]
    public void Invitation_Cancel_WhenNotPending_Throws()
    {
        var inv = EmployeeInvitation.Create(Guid.NewGuid(), "a@b.com", CompanyRole.Employee, Guid.NewGuid());
        inv.Accept(Guid.NewGuid());
        var act = () => inv.Cancel();
        act.Should().Throw<InvalidOperationException>().WithMessage("*cancel*");
    }

    // ── Waitlist residual ───────────────────────────────────────────────────

    [Fact]
    public void Waitlist_EmptyMembershipOrAllocation_Throws()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);

        var emptyMembership = () => CorporateWaitlistEntry.CreateEmployee(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), start, end, VehicleType.Car, "X", 1);
        emptyMembership.Should().Throw<ArgumentException>().WithMessage("*Membership*");

        var emptyAllocation = () => CorporateWaitlistEntry.CreateEmployee(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, start, end, VehicleType.Car, "X", 1);
        emptyAllocation.Should().Throw<ArgumentException>().WithMessage("*Allocation*");
    }

    [Fact]
    public void Waitlist_Promote_WhenNotPending_Throws()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var entry = CorporateWaitlistEntry.CreateEmployee(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start, end, VehicleType.Car, null, 1);
        entry.Cancel();

        var act = () => entry.Promote(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>().WithMessage("*promote*");
    }

    [Fact]
    public void Waitlist_LocalDateKind_Normalizes()
    {
        var startLocal = DateTime.SpecifyKind(new DateTime(2026, 7, 22, 10, 0, 0), DateTimeKind.Local);
        var endLocal = startLocal.AddHours(2);

        var entry = CorporateWaitlistEntry.CreateEmployee(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), startLocal, endLocal, VehicleType.Car, "ka", 2);

        entry.RequestedStartDateTime.Kind.Should().Be(DateTimeKind.Utc);
        entry.RequestedEndDateTime.Kind.Should().Be(DateTimeKind.Utc);
        entry.Overlaps(entry.RequestedStartDateTime.AddMinutes(10), entry.RequestedEndDateTime).Should().BeTrue();
    }

    // ── ParkingAllocation residual ──────────────────────────────────────────

    [Fact]
    public void Allocation_IsBookingAllowed_InvalidWindow_ReturnsFalse()
    {
        var allocation = ParkingAllocation.CreateCompanyOwned(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Quota.Create(2, 0, 2),
            0m,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid(),
            DefaultPolicy(weekends: false));
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc); // Wednesday

        // ArgumentException path (end <= start)
        allocation.IsBookingAllowed(1, start, start, 0, 0).Should().BeFalse();

        // InvalidOperationException path (before allowed hours 07:00)
        var early = new DateTime(2026, 7, 22, 5, 0, 0, DateTimeKind.Utc);
        allocation.IsBookingAllowed(1, early, early.AddHours(1), 0, 0).Should().BeFalse();

        // InvalidOperationException path (weekend not allowed)
        var saturday = new DateTime(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);
        allocation.IsBookingAllowed(1, saturday, saturday.AddHours(1), 0, 0).Should().BeFalse();
    }

    [Fact]
    public void Allocation_EnsureVisitor_Inactive_Throws()
    {
        var allocation = ParkingAllocation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Quota.Create(2, 0, 2),
            100m,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            DefaultPolicy());

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var act = () => allocation.EnsureVisitorBookingAllowed(start, start.AddHours(1));
        act.Should().Throw<InvalidOperationException>().WithMessage("*active allocation*");
    }

    [Fact]
    public void Allocation_EnsureVisitor_EndBeforeStart_Throws()
    {
        var company = CreateCompany(out var adminId);
        var allocation = ApproveOwned(company, adminId, Guid.NewGuid());
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var act = () => allocation.EnsureVisitorBookingAllowed(start, start.AddHours(-1));
        act.Should().Throw<ArgumentException>().WithMessage("*end time*");
    }

    [Fact]
    public void Allocation_BookingWithLocalKind_Normalizes()
    {
        // All-day policy so Local→UTC conversion cannot fail on hour windows.
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.Zero, new TimeSpan(23, 59, 59), true);
        var allocation = ParkingAllocation.CreateCompanyOwned(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Quota.Create(2, 0, 2),
            0m,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid(),
            policy);

        var startLocal = DateTime.SpecifyKind(new DateTime(2026, 7, 22, 12, 0, 0), DateTimeKind.Local);
        var endLocal = startLocal.AddHours(2);

        var act = () => allocation.EnsureVisitorBookingAllowed(startLocal, endLocal);
        act.Should().NotThrow();
    }

    // ── Invoice residual ────────────────────────────────────────────────────

    [Fact]
    public void Invoice_ExceedsMaxLineItems_Throws()
    {
        var lines = Enumerable.Range(0, CorporateInvoice.MaxLineItems + 1)
            .Select(i => new CorporateInvoiceLineDraft(CorporateInvoiceLineType.Usage, $"Line {i}", 1m, 1m))
            .ToList();

        var act = () => CorporateInvoice.Create(
            Guid.NewGuid(),
            BillingType.UsageBased,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            Guid.NewGuid(),
            lines);

        act.Should().Throw<InvalidOperationException>().WithMessage("*500*");
    }

    [Fact]
    public void Invoice_BlankCurrency_DefaultsToInr()
    {
        var invoice = CorporateInvoice.Create(
            Guid.NewGuid(),
            BillingType.UsageBased,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            Guid.NewGuid(),
            new[] { new CorporateInvoiceLineDraft(CorporateInvoiceLineType.Usage, "U", 1m, 10m) },
            currency: "  ");

        invoice.Currency.Should().Be(CorporateInvoice.DefaultCurrency);
    }

    // ── AccessPolicy / Quota residual ───────────────────────────────────────

    [Fact]
    public void AccessPolicy_EmptyPlate_NotAllowed_AndUnspecifiedKind()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Unspecified);
        var end = start.AddHours(2);
        var policy = AccessPolicy.Create("KA01XX", start, end, "QR-UNSPEC");

        policy.IsVehicleAllowed("").Should().BeFalse();
        policy.IsVehicleAllowed("   ").Should().BeFalse();
        policy.AccessStartUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Quota_Validation_Edges()
    {
        var zeroTotal = () => Quota.Create(0, 0, 0);
        zeroTotal.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("totalSlots");

        var negFixed = () => Quota.Create(5, -1, 0);
        negFixed.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("fixedSlots");

        var negShared = () => Quota.Create(5, 0, -1);
        negShared.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("sharedSlots");

        var exceed = () => Quota.Create(3, 2, 2);
        exceed.Should().Throw<ArgumentException>().WithMessage("*cannot exceed*");
    }
}
