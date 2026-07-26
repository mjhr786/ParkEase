using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>Wave 14 residual domain: allocation contract/windows, booking factories, value objects.</summary>
public class DomainResidualWave14Tests
{
    private static ParkingAllocation PendingVendor() =>
        ParkingAllocation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Quota.Create(4, 1, 3),
            2000m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));

    private static ParkingAllocation ActiveWithHours(bool weekends = true)
    {
        var policy = BookingPolicy.Create(
            2, 10, 1,
            TimeSpan.FromHours(9),
            TimeSpan.FromHours(17),
            allowWeekends: weekends);
        return ParkingAllocation.CreateCompanyOwned(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Quota.Create(3, 1, 2),
            0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid(),
            policy);
    }

    [Fact]
    public void Allocation_UpdateContractTerms_OnPending_Succeeds()
    {
        var allocation = PendingVendor();
        allocation.UpdateContractTerms(
            2500m,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            "  LEASE-X  ");

        allocation.MonthlyRate.Should().Be(2500m);
        allocation.LeaseReference.Should().Be("LEASE-X");
    }

    [Fact]
    public void Allocation_UpdateContractTerms_RejectedOrInvalid_Throws()
    {
        var allocation = PendingVendor();
        allocation.Reject("no");
        var act = () => allocation.UpdateContractTerms(
            100m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddMonths(1),
            null);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Rejected*");

        var active = ActiveWithHours();
        var badRate = () => active.UpdateContractTerms(-1, active.StartDate, active.EndDate, null);
        badRate.Should().Throw<ArgumentOutOfRangeException>();

        var badWindow = () => active.UpdateContractTerms(1, active.EndDate, active.StartDate, null);
        badWindow.Should().Throw<ArgumentException>().WithMessage("*after*");
    }

    [Fact]
    public void Allocation_UpdateBookingPolicy_Null_Throws()
    {
        var allocation = ActiveWithHours();
        var act = () => allocation.UpdateBookingPolicy(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Allocation_OutsideAllowedHours_Throws()
    {
        var allocation = ActiveWithHours(weekends: true);
        // Wednesday 18:00–19:00 UTC outside 09:00–17:00
        var start = new DateTime(2026, 7, 22, 18, 0, 0, DateTimeKind.Utc);
        var act = () => allocation.EnsureEmployeeBookingAllowed(1, start, start.AddHours(1), 0, 0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*allowed hours*");
    }

    [Fact]
    public void Allocation_WeekendNotAllowed_Throws()
    {
        var allocation = ActiveWithHours(weekends: false);
        // Saturday 10:00
        var start = new DateTime(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);
        var act = () => allocation.EnsureEmployeeBookingAllowed(1, start, start.AddHours(1), 0, 0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Weekend*");
    }

    [Fact]
    public void Allocation_PriorityThreshold_BlocksLowPriority()
    {
        var policy = BookingPolicy.Create(2, 10, priorityThreshold: 5,
            TimeSpan.FromHours(7), TimeSpan.FromHours(22), allowWeekends: true);
        var allocation = ParkingAllocation.CreateCompanyOwned(
            Guid.NewGuid(), Guid.NewGuid(), Quota.Create(2, 0, 2), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid(), policy);

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var act = () => allocation.EnsureEmployeeBookingAllowed(2, start, start.AddHours(1), 0, 0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*priority*");
    }

    [Fact]
    public void Allocation_HasFixedSlot_EmptyId_Throws()
    {
        var allocation = ActiveWithHours();
        var act = () => allocation.HasFixedSlotAssignment(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Allocation_RemoveFixed_WhenMissing_Throws()
    {
        var allocation = ActiveWithHours();
        var act = () => allocation.RemoveFixedAssignment(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>().WithMessage("*No fixed slot*");
    }

    [Fact]
    public void Allocation_SetVendorLeaseMetadata_AndApproveRejectGuards()
    {
        var allocation = PendingVendor();
        var vendorId = Guid.NewGuid();
        allocation.SetVendorLeaseMetadata(vendorId, " L1 ");
        allocation.VendorId.Should().Be(vendorId);
        allocation.LeaseReference.Should().Be("L1");

        allocation.Approve(Guid.NewGuid());
        var approveAgain = () => allocation.Approve(Guid.NewGuid());
        approveAgain.Should().Throw<InvalidOperationException>();

        var pending = PendingVendor();
        var emptyApprover = () => pending.Approve(Guid.Empty);
        emptyApprover.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CorporateBooking_VisitorRequiresNameAndPolicy()
    {
        var policy = AccessPolicy.Create("KA01", DateTime.UtcNow, DateTime.UtcNow.AddHours(2));
        var actName = () => CorporateBooking.CreateVisitorBooking(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "  ", "KA01", policy);
        actName.Should().Throw<ArgumentException>().WithMessage("*name*");

        var actPlate = () => CorporateBooking.CreateVisitorBooking(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Guest", "  ", policy);
        actPlate.Should().Throw<ArgumentException>().WithMessage("*plate*");

        var actPolicy = () => CorporateBooking.CreateVisitorBooking(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Guest", "KA01", null!);
        actPolicy.Should().Throw<ArgumentNullException>();

        var actIds = () => CorporateBooking.CreateEmployeeBooking(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CorporateSlotType.Shared);
        actIds.Should().Throw<ArgumentException>().WithMessage("*Company*");
    }

    [Fact]
    public void Quota_RejectsNegativeParts()
    {
        var negFixed = () => Quota.Create(5, -1, 3);
        negFixed.Should().Throw<ArgumentOutOfRangeException>();
        var negShared = () => Quota.Create(5, 1, -1);
        negShared.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BookingPolicy_Create_ValidatesLimits()
    {
        var weeklyLtDaily = () => BookingPolicy.Create(5, 3, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true);
        weeklyLtDaily.Should().Throw<ArgumentException>().WithMessage("*Weekly*");

        var badPriority = () => BookingPolicy.Create(1, 5, 0, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true);
        badPriority.Should().Throw<ArgumentOutOfRangeException>();

        var badHours = () => BookingPolicy.Create(1, 5, 1, TimeSpan.FromHours(22), TimeSpan.FromHours(7), true);
        badHours.Should().Throw<ArgumentException>().WithMessage("*end time*");
    }

    [Fact]
    public void AccessPolicy_ExplicitToken_AndPlateEmpty()
    {
        var start = DateTime.UtcNow;
        var end = start.AddHours(2);
        var policy = AccessPolicy.Create("ka01xx", start, end, "QR-CUSTOM");
        policy.QrCodeToken.Should().Be("QR-CUSTOM");
        policy.IsVehicleAllowed("").Should().BeFalse();
        policy.IsVehicleAllowed(null!).Should().BeFalse();
    }

    [Fact]
    public void EmployeeInvitation_Create_ValidatesArgs()
    {
        var emptyEmail = () => EmployeeInvitation.Create(Guid.NewGuid(), "  ", CompanyRole.Employee, Guid.NewGuid());
        emptyEmail.Should().Throw<ArgumentException>().WithMessage("*Email*");

        var emptyCompany = () => EmployeeInvitation.Create(Guid.Empty, "a@b.com", CompanyRole.Employee, Guid.NewGuid());
        emptyCompany.Should().Throw<ArgumentException>().WithMessage("*Company*");

        var badExpiry = () => EmployeeInvitation.Create(Guid.NewGuid(), "a@b.com", CompanyRole.Employee, Guid.NewGuid(), 0);
        badExpiry.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Waitlist_Visitor_InvalidAccessExpiry_Throws()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var act = () => CorporateWaitlistEntry.CreateVisitor(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            start, end, "Guest", "KA01",
            accessExpiryUtc: end.AddHours(-1),
            priorityAtRequest: 1);
        act.Should().Throw<ArgumentException>().WithMessage("*access expiry*");
    }

    [Fact]
    public void Company_AdminCanCancelOtherMembersWaitlist()
    {
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG-W14", "a@acme.com", "555", "Addr", BillingType.UsageBased, adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId, spaceId, Quota.Create(1, 0, 1), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            1, policy);

        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var draft = new CorporateBookingDraft(
            Guid.NewGuid(), spaceId, start, start.AddHours(2),
            ParkingApp.Marketplace.Contracts.Enums.BookingStatus.Confirmed, VehicleType.Car, "KA");
        var outcome = company.ReserveEmployeeParking(
            employeeId, allocation.Id, draft, 0, 0,
            new[] { 1 }, new Dictionary<int, int>(), 0, CorporateFraudAssessment.None());

        company.CancelWaitlistEntry(adminId, outcome.WaitlistEntry!.Id);
        outcome.WaitlistEntry.Status.Should().Be(WaitlistStatus.Cancelled);
    }

    [Fact]
    public void CompanyUsage_Create_ValidatesIds()
    {
        var act = () => CompanyUsage.Create(Guid.Empty, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        act.Should().Throw<ArgumentException>().WithMessage("*Company*");
    }

    [Fact]
    public void Membership_SetPriority_OutOfRange_Throws()
    {
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG-P", "a@acme.com", "555", "Addr", BillingType.UsageBased, adminId);
        var m = company.Memberships.First();
        var act = () => m.SetPriority(11);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
