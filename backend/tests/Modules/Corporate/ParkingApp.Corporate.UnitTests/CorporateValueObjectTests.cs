using FluentAssertions;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.UnitTests;

public class CorporateValueObjectTests
{
    [Fact]
    public void Quota_Create_ComputesUnallocated()
    {
        var quota = Quota.Create(totalSlots: 10, fixedSlots: 3, sharedSlots: 5);
        quota.UnallocatedSlots.Should().Be(2);
        quota.HasFixedSlots.Should().BeTrue();
        quota.HasSharedSlots.Should().BeTrue();
    }

    [Fact]
    public void Quota_Create_RejectsInvalidSplit()
    {
        var act = () => Quota.Create(5, 4, 3);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BookingPolicy_Default_AllowsWeekdayLimits()
    {
        var policy = BookingPolicy.Default();
        policy.IsWithinDailyLimit(0).Should().BeTrue();
        policy.IsWithinDailyLimit(1).Should().BeFalse();
        policy.IsWithinWeeklyLimit(4).Should().BeTrue();
        policy.IsWithinWeeklyLimit(5).Should().BeFalse();
    }

    [Fact]
    public void BookingPolicy_Weekend_Respected()
    {
        var noWeekends = BookingPolicy.Create(
            maxBookingsPerDay: 2,
            maxBookingsPerWeek: 10,
            priorityThreshold: 1,
            allowedStartTime: TimeSpan.FromHours(7),
            allowedEndTime: TimeSpan.FromHours(22),
            allowWeekends: false);
        // Saturday
        var saturday = new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);
        noWeekends.IsWeekendAllowed(saturday).Should().BeFalse();

        var withWeekends = BookingPolicy.Create(
            2, 10, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), allowWeekends: true);
        withWeekends.IsWeekendAllowed(saturday).Should().BeTrue();
    }

    [Fact]
    public void BookingPolicy_PriorityThreshold()
    {
        var policy = BookingPolicy.Create(
            1, 5, priorityThreshold: 3,
            TimeSpan.FromHours(7), TimeSpan.FromHours(22), allowWeekends: true);
        policy.MeetsPriorityRequirement(3).Should().BeTrue();
        policy.MeetsPriorityRequirement(2).Should().BeFalse();
    }

    [Fact]
    public void AccessPolicy_IsActiveAndPlateMatch()
    {
        var start = DateTime.UtcNow.AddHours(-1);
        var end = DateTime.UtcNow.AddHours(2);
        var policy = AccessPolicy.Create("ka01ab1234", start, end);

        policy.IsActive(DateTime.UtcNow).Should().BeTrue();
        policy.IsExpired(DateTime.UtcNow).Should().BeFalse();
        policy.IsVehicleAllowed("KA01AB1234").Should().BeTrue();
        policy.IsVehicleAllowed("KA99ZZ9999").Should().BeFalse();
        policy.IsExpired(end.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void ParkingAllocation_Create_AndApprove()
    {
        var companyId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var allocation = ParkingAllocation.Create(
            companyId,
            spaceId,
            Quota.Create(5, 1, 4),
            monthlyRate: 5000m,
            startDate: DateTime.UtcNow.Date,
            endDate: DateTime.UtcNow.Date.AddMonths(6));

        allocation.Status.Should().Be(AllocationStatus.PendingApproval);
        allocation.MonthlyRate.Should().Be(5000m);

        var approver = Guid.NewGuid();
        allocation.Approve(approver);
        allocation.Status.Should().Be(AllocationStatus.Active);
        allocation.ApprovedByUserId.Should().Be(approver);
    }

    [Fact]
    public void ParkingAllocation_Reject_FromPending()
    {
        var allocation = ParkingAllocation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Quota.Create(2, 0, 2),
            0m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddMonths(1));

        allocation.Reject("No capacity");
        allocation.Status.Should().Be(AllocationStatus.Rejected);
        allocation.RejectionReason.Should().Be("No capacity");
    }
}
