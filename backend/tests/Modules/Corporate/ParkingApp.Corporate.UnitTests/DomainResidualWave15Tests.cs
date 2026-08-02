using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using BookingStatus = ParkingApp.Marketplace.Contracts.Enums.BookingStatus;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>
/// Wave 15 residual domain: FixedSlotAssignment factory, allocation assign/remove edges,
/// shared-slot resolution edge cases, draft/fraud factories, waitlist validation.
/// </summary>
public class DomainResidualWave15Tests
{
    private static ParkingAllocation ActiveOwned(
        Guid companyId,
        int fixedSlots = 2,
        int sharedSlots = 3,
        bool weekends = true)
    {
        var policy = BookingPolicy.Create(
            2, 10, 1,
            TimeSpan.FromHours(7),
            TimeSpan.FromHours(22),
            allowWeekends: weekends);
        return ParkingAllocation.CreateCompanyOwned(
            companyId,
            Guid.NewGuid(),
            Quota.Create(fixedSlots + sharedSlots, fixedSlots, sharedSlots),
            0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Guid.NewGuid(),
            policy);
    }

    private static UserCompanyMembership ActiveMember(Guid companyId, Guid? userId = null) =>
        UserCompanyMembership.Create(companyId, userId ?? Guid.NewGuid(), CompanyRole.Employee);

    // ── FixedSlotAssignment factory (internal; InternalsVisibleTo) ──────────

    [Fact]
    public void FixedSlotAssignment_Create_Succeeds()
    {
        var companyId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();

        var assignment = FixedSlotAssignment.Create(
            companyId, allocationId, membershipId, ParkingApp.BuildingBlocks.Enums.VehicleClass.FourWheeler, 2);

        assignment.CompanyId.Should().Be(companyId);
        assignment.AllocationId.Should().Be(allocationId);
        assignment.MembershipId.Should().Be(membershipId);
        assignment.SlotNumber.Should().Be(2);
        assignment.AssignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void FixedSlotAssignment_Create_EmptyIds_Throw(bool emptyCompany, bool emptyAllocation, bool emptyMembership)
    {
        var act = () => FixedSlotAssignment.Create(
            emptyCompany ? Guid.Empty : Guid.NewGuid(),
            emptyAllocation ? Guid.Empty : Guid.NewGuid(),
            emptyMembership ? Guid.Empty : Guid.NewGuid(),
            ParkingApp.BuildingBlocks.Enums.VehicleClass.FourWheeler,
            1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FixedSlotAssignment_Create_NonPositiveSlot_Throws()
    {
        var act = () => FixedSlotAssignment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            ParkingApp.BuildingBlocks.Enums.VehicleClass.FourWheeler, 0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("slotNumber");
    }

    // ── ParkingAllocation fixed-slot assign / remove ────────────────────────

    [Fact]
    public void Allocation_AssignFixedSlot_HappyPath_HasAndRemove()
    {
        var companyId = Guid.NewGuid();
        var allocation = ActiveOwned(companyId, fixedSlots: 2, sharedSlots: 2);
        var member = ActiveMember(companyId);

        allocation.AssignFixedSlot(member, 1);
        allocation.HasFixedSlotAssignment(member.Id).Should().BeTrue();
        allocation.FixedAssignments.Should().ContainSingle(a => a.SlotNumber == 1 && !a.IsDeleted);

        var reservation = allocation.ResolveSlotReservation(
            member.Id,
            Array.Empty<int>(),
            new Dictionary<int, int>());
        reservation.SlotType.Should().Be(CorporateSlotType.Fixed);
        reservation.SlotNumber.Should().Be(1);

        allocation.RemoveFixedAssignment(member.Id);
        allocation.HasFixedSlotAssignment(member.Id).Should().BeFalse();
        allocation.FixedAssignments.Single(a => a.MembershipId == member.Id).IsDeleted.Should().BeTrue();

        // Slot reusable after soft-delete
        allocation.AssignFixedSlot(member, 1);
        allocation.HasFixedSlotAssignment(member.Id).Should().BeTrue();
    }

    [Fact]
    public void Allocation_AssignFixedSlot_NullMembership_Throws()
    {
        var allocation = ActiveOwned(Guid.NewGuid());
        var act = () => allocation.AssignFixedSlot(null!, 1);
        act.Should().Throw<ArgumentNullException>().WithParameterName("membership");
    }

    [Fact]
    public void Allocation_AssignFixedSlot_InactiveAllocation_Throws()
    {
        var companyId = Guid.NewGuid();
        var allocation = ActiveOwned(companyId);
        allocation.Expire();
        var member = ActiveMember(companyId);

        var act = () => allocation.AssignFixedSlot(member, 1);
        act.Should().Throw<InvalidOperationException>().WithMessage("*active*");
    }

    [Fact]
    public void Allocation_AssignFixedSlot_WrongCompany_Throws()
    {
        var allocation = ActiveOwned(Guid.NewGuid());
        var otherMember = ActiveMember(Guid.NewGuid());

        var act = () => allocation.AssignFixedSlot(otherMember, 1);
        act.Should().Throw<InvalidOperationException>().WithMessage("*same company*");
    }

    [Fact]
    public void Allocation_AssignFixedSlot_InactiveMember_Throws()
    {
        var companyId = Guid.NewGuid();
        var allocation = ActiveOwned(companyId);
        var member = ActiveMember(companyId);
        member.Deactivate();

        var act = () => allocation.AssignFixedSlot(member, 1);
        act.Should().Throw<InvalidOperationException>().WithMessage("*active company members*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Allocation_AssignFixedSlot_OutOfRange_Throws(int slot)
    {
        var companyId = Guid.NewGuid();
        var allocation = ActiveOwned(companyId, fixedSlots: 2, sharedSlots: 1);
        var member = ActiveMember(companyId);

        var act = () => allocation.AssignFixedSlot(member, slot);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("slotNumber");
    }

    [Fact]
    public void Allocation_AssignFixedSlot_DuplicateSlotOrMember_Throws()
    {
        var companyId = Guid.NewGuid();
        var allocation = ActiveOwned(companyId, fixedSlots: 2, sharedSlots: 1);
        var m1 = ActiveMember(companyId);
        var m2 = ActiveMember(companyId);

        allocation.AssignFixedSlot(m1, 1);

        var slotTaken = () => allocation.AssignFixedSlot(m2, 1);
        slotTaken.Should().Throw<InvalidOperationException>().WithMessage("*already assigned*");

        var memberHasSlot = () => allocation.AssignFixedSlot(m1, 2);
        memberHasSlot.Should().Throw<InvalidOperationException>().WithMessage("*already has a fixed slot*");
    }

    [Fact]
    public void Company_AssignFixedSlot_AsAdmin_DelegatesToAllocation()
    {
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme W15", "REG-W15", "w15@acme.com", "555", "Addr", BillingType.UsageBased, adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId, Guid.NewGuid(), Quota.Create(4, 2, 2), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            4, policy);

        company.AssignFixedSlot(adminId, allocation.Id, membership.Id, 2);

        allocation.HasFixedSlotAssignment(membership.Id).Should().BeTrue();
        allocation.FixedAssignments.Should().Contain(a => a.SlotNumber == 2 && a.MembershipId == membership.Id);
    }

    // ── Shared-slot resolution edges ────────────────────────────────────────

    [Fact]
    public void Allocation_ResolveSlotReservation_EmptyMembership_Throws()
    {
        var allocation = ActiveOwned(Guid.NewGuid(), fixedSlots: 0, sharedSlots: 2);
        var act = () => allocation.ResolveSlotReservation(
            Guid.Empty, Array.Empty<int>(), new Dictionary<int, int>());
        act.Should().Throw<ArgumentException>().WithMessage("*Membership*");
    }

    [Fact]
    public void Allocation_ResolveSharedSlot_Inactive_Throws()
    {
        var allocation = ActiveOwned(Guid.NewGuid(), fixedSlots: 0, sharedSlots: 2);
        allocation.Expire();
        var act = () => allocation.ResolveSharedSlotReservation(
            Array.Empty<int>(), new Dictionary<int, int>());
        act.Should().Throw<InvalidOperationException>().WithMessage("*active*");
    }

    [Fact]
    public void Allocation_ResolveSharedSlot_ZeroShared_Throws()
    {
        var allocation = ActiveOwned(Guid.NewGuid(), fixedSlots: 2, sharedSlots: 0);
        var act = () => allocation.ResolveSharedSlotReservation(
            Array.Empty<int>(), new Dictionary<int, int>());
        act.Should().Throw<InvalidOperationException>().WithMessage("*No shared*");
    }

    [Fact]
    public void Allocation_ResolveSharedSlot_SkipsAnonymousOccupancy()
    {
        // Shared slots are numbered after fixed: fixed=0 → shared 1,2,3
        var allocation = ActiveOwned(Guid.NewGuid(), fixedSlots: 0, sharedSlots: 3);
        var usage = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0 };

        // anonymousOccupiedSharedBookings=1 skips the first ordered candidate
        var reservation = allocation.ResolveSharedSlotReservation(
            occupiedSharedSlotNumbers: Array.Empty<int>(),
            sharedSlotUsageBySlot: usage,
            anonymousOccupiedSharedBookings: 1);

        reservation.SlotType.Should().Be(CorporateSlotType.Shared);
        reservation.SlotNumber.Should().Be(2);
    }

    [Fact]
    public void Allocation_ResolveSharedSlot_NullCollections_TreatedAsEmpty()
    {
        var allocation = ActiveOwned(Guid.NewGuid(), fixedSlots: 0, sharedSlots: 2);
        var reservation = allocation.ResolveSharedSlotReservation(null!, null!);
        reservation.SlotNumber.Should().Be(1);
    }

    [Fact]
    public void Allocation_IsBookingAllowed_WeeklyLimit_ReturnsFalse()
    {
        var allocation = ActiveOwned(Guid.NewGuid(), fixedSlots: 0, sharedSlots: 2);
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc); // Wednesday
        allocation.IsBookingAllowed(1, start, start.AddHours(1), currentDayBookings: 0, currentWeekBookings: 10)
            .Should().BeFalse();
    }

    [Fact]
    public void Allocation_Create_ValidationEdges()
    {
        var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var quota = Quota.Create(2, 0, 2);

        var emptyCompany = () => ParkingAllocation.Create(Guid.Empty, Guid.NewGuid(), quota, 0m, start, end);
        emptyCompany.Should().Throw<ArgumentException>().WithMessage("*Company*");

        var emptySpace = () => ParkingAllocation.Create(Guid.NewGuid(), Guid.Empty, quota, 0m, start, end);
        emptySpace.Should().Throw<ArgumentException>().WithMessage("*Parking*");

        var negRate = () => ParkingAllocation.Create(Guid.NewGuid(), Guid.NewGuid(), quota, -1m, start, end);
        negRate.Should().Throw<ArgumentOutOfRangeException>();

        var badWindow = () => ParkingAllocation.Create(Guid.NewGuid(), Guid.NewGuid(), quota, 0m, end, start);
        badWindow.Should().Throw<ArgumentException>().WithMessage("*after*");

        var nullQuota = () => ParkingAllocation.Create(Guid.NewGuid(), Guid.NewGuid(), null!, 0m, start, end);
        nullQuota.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Allocation_SetVendorLeaseMetadata_Guards()
    {
        var owned = ActiveOwned(Guid.NewGuid());
        var onOwned = () => owned.SetVendorLeaseMetadata(Guid.NewGuid(), "X");
        onOwned.Should().Throw<InvalidOperationException>().WithMessage("*vendor-leased*");

        var pending = ParkingAllocation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Quota.Create(2, 0, 2), 100m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        var emptyVendor = () => pending.SetVendorLeaseMetadata(Guid.Empty, "L");
        emptyVendor.Should().Throw<ArgumentException>().WithMessage("*Vendor*");

        pending.SetVendorLeaseMetadata(Guid.NewGuid(), "   ");
        pending.LeaseReference.Should().BeNull();
    }

    [Fact]
    public void Allocation_Reject_WrongStatus_Throws()
    {
        var owned = ActiveOwned(Guid.NewGuid());
        var act = () => owned.Reject("no");
        act.Should().Throw<InvalidOperationException>().WithMessage("*reject*");
    }

    [Fact]
    public void Allocation_BookingWindow_UnspecifiedKind_Normalizes()
    {
        var allocation = ActiveOwned(Guid.NewGuid(), fixedSlots: 0, sharedSlots: 2);
        // Unspecified Kind → treated as UTC by NormalizeDate
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Unspecified);
        var end = new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Unspecified);
        var act = () => allocation.EnsureVisitorBookingAllowed(start, end);
        act.Should().NotThrow();
    }

    // ── Fraud / slot reservation factories ──────────────────────────────────

    [Fact]
    public void CorporateFraudAssessment_Factories()
    {
        var none = CorporateFraudAssessment.None();
        none.IsBlocked.Should().BeFalse();
        none.RiskLevel.Should().Be(CorporateFraudRiskLevel.None);
        none.Reason.Should().BeNull();

        var flag = CorporateFraudAssessment.Flag(CorporateFraudRiskLevel.Low, "slow");
        flag.IsBlocked.Should().BeFalse();
        flag.Reason.Should().Be("slow");

        var block = CorporateFraudAssessment.Block(CorporateFraudRiskLevel.High, "bad");
        block.IsBlocked.Should().BeTrue();
        block.RiskLevel.Should().Be(CorporateFraudRiskLevel.High);
    }

    [Fact]
    public void CorporateSlotReservation_Factories()
    {
        var f = CorporateSlotReservation.Fixed(3);
        f.SlotType.Should().Be(CorporateSlotType.Fixed);
        f.SlotNumber.Should().Be(3);

        var s = CorporateSlotReservation.Shared(7);
        s.SlotType.Should().Be(CorporateSlotType.Shared);
        s.SlotNumber.Should().Be(7);
    }

    // ── Draft / usage / waitlist residual ───────────────────────────────────

    [Fact]
    public void CorporateBookingDraft_ValidationAndPendingConfirm()
    {
        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddHours(2);

        var emptyBooking = () => new CorporateBookingDraft(
            Guid.Empty, Guid.NewGuid(), start, end, BookingStatus.Pending, VehicleType.Car, "ka");
        emptyBooking.Should().Throw<ArgumentException>().WithMessage("*Booking*");

        var emptySpace = () => new CorporateBookingDraft(
            Guid.NewGuid(), Guid.Empty, start, end, BookingStatus.Pending, VehicleType.Car, "ka");
        emptySpace.Should().Throw<ArgumentException>().WithMessage("*Parking*");

        var badWindow = () => new CorporateBookingDraft(
            Guid.NewGuid(), Guid.NewGuid(), end, start, BookingStatus.Pending, VehicleType.Car, "ka");
        badWindow.Should().Throw<ArgumentException>().WithMessage("*after*");

        var draft = new CorporateBookingDraft(
            Guid.NewGuid(), Guid.NewGuid(), start, end, BookingStatus.Pending, VehicleType.Car, "  ab12  ");
        draft.VehicleNumber.Should().Be("AB12");
        draft.DurationHours.Should().BeApproximately(2.0, 0.01);

        var adj = draft.ToConfirmationAdjustment(5);
        adj.ShouldConfirm.Should().BeTrue();
        adj.RequiresConfirmedStatus.Should().BeFalse();
        adj.SlotNumber.Should().Be(5);
    }

    [Fact]
    public void CompanyUsage_EmptyAllocationId_Throws()
    {
        var act = () => CompanyUsage.Create(Guid.NewGuid(), Guid.Empty, DateOnly.FromDateTime(DateTime.UtcNow));
        act.Should().Throw<ArgumentException>().WithMessage("*Allocation*");
    }

    [Fact]
    public void Waitlist_PromoteEmptyBooking_AndPriorityGuards()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var entry = CorporateWaitlistEntry.CreateEmployee(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start, end, VehicleType.Car, null, 1);

        var emptyBooking = () => entry.Promote(Guid.Empty);
        emptyBooking.Should().Throw<ArgumentException>().WithMessage("*Booking*");

        var badPriority = () => CorporateWaitlistEntry.CreateEmployee(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start, end, VehicleType.Car, "X", 0);
        badPriority.Should().Throw<ArgumentOutOfRangeException>();

        var emptyCompany = () => CorporateWaitlistEntry.CreateEmployee(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), start, end, VehicleType.Car, "X", 1);
        emptyCompany.Should().Throw<ArgumentException>().WithMessage("*Company*");

        var emptyVisitorName = () => CorporateWaitlistEntry.CreateVisitor(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start, end, "  ", "KA01", end.AddHours(1), 1);
        emptyVisitorName.Should().Throw<ArgumentException>().WithMessage("*name*");

        var emptyPlate = () => CorporateWaitlistEntry.CreateVisitor(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start, end, "Guest", "  ", end.AddHours(1), 1);
        emptyPlate.Should().Throw<ArgumentException>().WithMessage("*plate*");
    }

    [Fact]
    public void CorporateBooking_Employee_EmptyMembershipOrBooking_Throws()
    {
        var emptyMembership = () => CorporateBooking.CreateEmployeeBooking(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), CorporateSlotType.Shared);
        emptyMembership.Should().Throw<ArgumentException>().WithMessage("*Membership*");

        var emptyBooking = () => CorporateBooking.CreateEmployeeBooking(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, CorporateSlotType.Fixed);
        emptyBooking.Should().Throw<ArgumentException>().WithMessage("*Booking*");

        var emptyAllocation = () => CorporateBooking.CreateEmployeeBooking(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), CorporateSlotType.Shared);
        emptyAllocation.Should().Throw<ArgumentException>().WithMessage("*Allocation*");
    }

    [Fact]
    public void Quota_ExceedsTotal_AndUnallocated()
    {
        var act = () => Quota.Create(5, 3, 3);
        act.Should().Throw<ArgumentException>().WithMessage("*exceed*");

        var q = Quota.Create(10, 2, 5);
        q.UnallocatedSlots.Should().Be(3);
        q.HasSharedSlots.Should().BeTrue();
    }

    [Fact]
    public void BookingPolicy_DailyAndWeeklyCreateGuards()
    {
        var badDaily = () => BookingPolicy.Create(0, 5, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true);
        badDaily.Should().Throw<ArgumentOutOfRangeException>();

        var badWeekly = () => BookingPolicy.Create(1, 0, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true);
        badWeekly.Should().Throw<ArgumentOutOfRangeException>();

        var def = BookingPolicy.Default();
        def.MaxBookingsPerEmployeePerDay.Should().Be(1);
        def.IsWithinDailyLimit(0).Should().BeTrue();
        def.IsWithinWeeklyLimit(4).Should().BeTrue();
        def.IsWithinWeeklyLimit(5).Should().BeFalse();
    }

    [Fact]
    public void Invitation_AcceptEmptyUser_AndResendGuards()
    {
        var inv = EmployeeInvitation.Create(Guid.NewGuid(), "a@b.com", CompanyRole.Employee, Guid.NewGuid());
        var emptyUser = () => inv.Accept(Guid.Empty);
        emptyUser.Should().Throw<ArgumentException>().WithMessage("*User*");

        inv.Accept(Guid.NewGuid());
        var resendAccepted = () => inv.Resend(7);
        resendAccepted.Should().Throw<InvalidOperationException>().WithMessage("*Accepted*");

        var emptyInviter = () => EmployeeInvitation.Create(
            Guid.NewGuid(), "c@d.com", CompanyRole.Employee, Guid.Empty);
        emptyInviter.Should().Throw<ArgumentException>().WithMessage("*Inviter*");
    }

    [Fact]
    public void Membership_Create_ValidatesIdsAndPriority()
    {
        var emptyCompany = () => UserCompanyMembership.Create(Guid.Empty, Guid.NewGuid(), CompanyRole.Employee);
        emptyCompany.Should().Throw<ArgumentException>().WithMessage("*Company*");

        var emptyUser = () => UserCompanyMembership.Create(Guid.NewGuid(), Guid.Empty, CompanyRole.Employee);
        emptyUser.Should().Throw<ArgumentException>().WithMessage("*User*");

        var badPriority = () => UserCompanyMembership.Create(Guid.NewGuid(), Guid.NewGuid(), CompanyRole.Employee, priority: 11);
        badPriority.Should().Throw<ArgumentOutOfRangeException>();
    }
}
