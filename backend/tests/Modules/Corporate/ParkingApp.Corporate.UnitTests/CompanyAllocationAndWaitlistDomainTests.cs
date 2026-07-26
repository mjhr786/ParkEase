using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.UnitTests;

public class CompanyAllocationAndWaitlistDomainTests
{
    private static Company CreateCompany(out Guid adminId)
    {
        adminId = Guid.NewGuid();
        return Company.Create("Acme", "REG-A1", "a@acme.com", "555", "Addr", BillingType.ReservedSlots, adminId);
    }

    [Fact]
    public void RequestAllocation_WhenAdmin_AddsPendingAllocation()
    {
        var company = CreateCompany(out var adminId);
        var spaceId = Guid.NewGuid();

        var allocation = company.RequestAllocation(
            adminId,
            spaceId,
            Quota.Create(5, 1, 4),
            monthlyRate: 2500m,
            startDate: DateTime.UtcNow.Date,
            endDate: DateTime.UtcNow.Date.AddMonths(6),
            parkingCapacity: 10);

        allocation.Status.Should().Be(AllocationStatus.PendingApproval);
        allocation.ParkingSpaceId.Should().Be(spaceId);
        company.Allocations.Should().Contain(allocation);
    }

    [Fact]
    public void RequestAllocation_WhenQuotaExceedsCapacity_Throws()
    {
        var company = CreateCompany(out var adminId);

        var act = () => company.RequestAllocation(
            adminId,
            Guid.NewGuid(),
            Quota.Create(20, 5, 15),
            1000m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddMonths(1),
            parkingCapacity: 10);

        act.Should().Throw<InvalidOperationException>().WithMessage("*10*");
    }

    [Fact]
    public void RequestAllocation_WhenNonAdmin_Throws()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(adminId, employeeId, CompanyRole.Employee);

        var act = () => company.RequestAllocation(
            employeeId,
            Guid.NewGuid(),
            Quota.Create(2, 0, 2),
            0m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddMonths(1),
            parkingCapacity: 5);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void CreateOwnedParkingAllocation_IsActiveImmediately()
    {
        var company = CreateCompany(out var adminId);

        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            Guid.NewGuid(),
            Quota.Create(3, 0, 3),
            0m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddYears(1),
            parkingCapacity: 3);

        allocation.Status.Should().Be(AllocationStatus.Active);
        allocation.SourceType.Should().Be(ParkingAllocationSource.CompanyOwned);
    }

    [Fact]
    public void ApproveAndRejectAllocation_ThroughCompany()
    {
        var company = CreateCompany(out var adminId);
        var allocation = company.RequestAllocation(
            adminId,
            Guid.NewGuid(),
            Quota.Create(2, 0, 2),
            500m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddMonths(3),
            parkingCapacity: 5);

        var ownerId = Guid.NewGuid();
        company.ApproveAllocation(allocation.Id, ownerId);
        allocation.Status.Should().Be(AllocationStatus.Active);

        var pending = company.RequestAllocation(
            adminId,
            Guid.NewGuid(),
            Quota.Create(1, 0, 1),
            100m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddMonths(1),
            parkingCapacity: 5);
        company.RejectAllocation(pending.Id, "No deal");
        pending.Status.Should().Be(AllocationStatus.Rejected);
        pending.RejectionReason.Should().Be("No deal");
    }

    [Fact]
    public void CancelWaitlistEntry_ByRequester_Succeeds()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            Guid.NewGuid(),
            Quota.Create(2, 0, 2),
            0m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddMonths(1),
            parkingCapacity: 2);

        var entry = CorporateWaitlistEntry.CreateEmployee(
            company.Id,
            membership.Id,
            allocation.Id,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(5),
            VehicleType.Car,
            "KA01AB1234",
            priorityAtRequest: membership.Priority);
        company.WaitlistEntries.Add(entry);

        company.CancelWaitlistEntry(employeeId, entry.Id);
        entry.Status.Should().Be(WaitlistStatus.Cancelled);
    }

    [Fact]
    public void CancelWaitlistEntry_WhenOtherEmployee_Throws()
    {
        var company = CreateCompany(out var adminId);
        var employeeId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var membership = company.AddMember(adminId, employeeId, CompanyRole.Employee);
        company.AddMember(adminId, otherId, CompanyRole.Employee);
        var allocation = company.CreateOwnedParkingAllocation(
            adminId,
            Guid.NewGuid(),
            Quota.Create(2, 0, 2),
            0m,
            DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddMonths(1),
            parkingCapacity: 2);

        var entry = CorporateWaitlistEntry.CreateEmployee(
            company.Id,
            membership.Id,
            allocation.Id,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(5),
            VehicleType.Car,
            null,
            priorityAtRequest: 1);
        company.WaitlistEntries.Add(entry);

        var act = () => company.CancelWaitlistEntry(otherId, entry.Id);
        act.Should().Throw<InvalidOperationException>().WithMessage("*admin*");
    }
}
