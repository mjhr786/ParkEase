using FluentAssertions;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.UnitTests;

public class CompanyDomainEdgeTests
{
    private static Company Create(out Guid adminId)
    {
        adminId = Guid.NewGuid();
        return Company.Create("Acme Edge", "REG-EDGE", "edge@acme.com", "555", "Addr", BillingType.UsageBased, adminId);
    }

    [Fact]
    public void PreCheck_AnonymousOccupiedSharedBookings_ClampsAtZero()
    {
        var pre = new CorporateReservationPreCheck
        {
            DayBookingCount = 0,
            WeekBookingCount = 0,
            ActiveSharedBookingCount = 2,
            OccupiedSharedSlotNumbers = new[] { 1, 2, 3 },
            SharedSlotUsageBySlot = new Dictionary<int, int>(),
            HasOverlappingMemberBooking = false,
            HasOverlappingVehicleBooking = false,
            RecentBookingCreateCount = 0
        };

        pre.AnonymousOccupiedSharedBookings.Should().Be(0);
    }

    [Fact]
    public void PreCheck_AnonymousOccupiedSharedBookings_CountsDifference()
    {
        var pre = new CorporateReservationPreCheck
        {
            DayBookingCount = 1,
            WeekBookingCount = 2,
            ActiveSharedBookingCount = 5,
            OccupiedSharedSlotNumbers = new[] { 1, 2 },
            SharedSlotUsageBySlot = new Dictionary<int, int> { [1] = 3 },
            HasOverlappingMemberBooking = false,
            HasOverlappingVehicleBooking = false,
            RecentBookingCreateCount = 0
        };

        pre.AnonymousOccupiedSharedBookings.Should().Be(3);
    }

    [Fact]
    public void AssessFraud_BlocksOverlappingMemberBooking()
    {
        var company = Create(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddHours(2);

        var result = company.AssessFraudRisk(employeeId, start, end, true, false, 0);

        result.IsBlocked.Should().BeTrue();
        result.RiskLevel.Should().Be(CorporateFraudRiskLevel.High);
        result.Reason.Should().Contain("overlapping corporate booking");
    }

    [Fact]
    public void AssessFraud_BlocksOverlappingVehicle()
    {
        var company = Create(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddHours(2);

        var result = company.AssessFraudRisk(employeeId, start, end, false, true, 0);

        result.IsBlocked.Should().BeTrue();
        result.Reason.Should().Contain("vehicle");
    }

    [Fact]
    public void AssessFraud_FlagsElevatedAndHighFrequency()
    {
        var company = Create(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddHours(2);

        var low = company.AssessFraudRisk(employeeId, start, end, false, false, 3);
        low.IsBlocked.Should().BeFalse();
        low.RiskLevel.Should().Be(CorporateFraudRiskLevel.Low);

        var medium = company.AssessFraudRisk(employeeId, start, end, false, false, 6);
        medium.IsBlocked.Should().BeFalse();
        medium.RiskLevel.Should().Be(CorporateFraudRiskLevel.Medium);

        var none = company.AssessFraudRisk(employeeId, start, end, false, false, 0);
        none.RiskLevel.Should().Be(CorporateFraudRiskLevel.None);
    }

    [Fact]
    public void AssessFraud_InvalidWindow_Throws()
    {
        var company = Create(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var start = DateTime.UtcNow;

        var act = () => company.AssessFraudRisk(employeeId, start, start, false, false, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateBookingAmount_UsageBased_CeilsHours()
    {
        var company = Create(out _);
        var amount = company.CalculateBookingAmount(100m, TimeSpan.FromMinutes(90));
        amount.Should().Be(200m);
    }

    [Fact]
    public void CalculateBookingAmount_ReservedSlots_IsZero()
    {
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG-RS", "a@acme.com", "555", "Addr", BillingType.ReservedSlots, adminId);
        company.CalculateBookingAmount(100m, TimeSpan.FromHours(2)).Should().Be(0m);
    }

    [Fact]
    public void Deactivate_Activate_AndUpdateBillingType()
    {
        var company = Create(out var adminId);
        company.Deactivate();
        company.IsActive.Should().BeFalse();
        company.Activate();
        company.IsActive.Should().BeTrue();
        company.UpdateBillingType(BillingType.ReservedSlots);
        company.BillingType.Should().Be(BillingType.ReservedSlots);
    }

    [Fact]
    public void UpdateProfile_AsAdmin_UpdatesFields()
    {
        var company = Create(out var adminId);
        company.UpdateProfile(adminId, "New Name", "NEW@acme.com", "999", "New Addr", BillingType.ReservedSlots);

        company.Name.Should().Be("New Name");
        company.ContactEmail.Should().Be("new@acme.com");
        company.ContactPhone.Should().Be("999");
        company.BillingAddress.Should().Be("New Addr");
        company.BillingType.Should().Be(BillingType.ReservedSlots);
    }

    [Fact]
    public void Invite_Cancel_AndResend_Invitation()
    {
        var company = Create(out var adminId);
        var invitation = company.InviteMember(adminId, "new@hire.com", CompanyRole.Employee);
        invitation.Status.Should().Be(InvitationStatus.Pending);

        var resent = company.ResendInvitation(adminId, invitation.Id, expiresInDays: 14);
        resent.Id.Should().Be(invitation.Id);
        resent.Status.Should().Be(InvitationStatus.Pending);

        company.CancelInvitation(adminId, invitation.Id);
        invitation.Status.Should().Be(InvitationStatus.Cancelled);

        var act = () => company.ResendInvitation(adminId, invitation.Id);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Cancelled*");
    }

    [Fact]
    public void Quota_UnallocatedAndZeroFixedSharedFlags()
    {
        var quota = Quota.Create(5, 0, 0);
        quota.UnallocatedSlots.Should().Be(5);
        quota.HasFixedSlots.Should().BeFalse();
        quota.HasSharedSlots.Should().BeFalse();
    }

    [Fact]
    public void AccessPolicy_RejectsEmptyPlate()
    {
        var act = () => AccessPolicy.Create("  ", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        act.Should().Throw<ArgumentException>();
    }
}
