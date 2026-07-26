using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Allocations;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Corporate.UnitTests;

public class OwnedAllocationAndPolicyHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IParkingSpaceLookup> _parking = new();
    private readonly Mock<ICompanyQuotaCache> _quota = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public OwnedAllocationAndPolicyHandlerTests()
    {
        _uow.Setup(x => x.Companies).Returns(_companies.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _quota.Setup(x => x.InvalidateCompanyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    private static ParkingSpaceSummary OwnedSpace(Guid id, Guid companyId, int spots = 10) =>
        new(id, Guid.NewGuid(), "Owned Lot", true, spots, "CompanyOwned", companyId);

    [Fact]
    public async Task CreateOwned_WhenNotCompanyOwned_Fails()
    {
        var company = Company.Create("Acme", "REG-OA1", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var spaceId = Guid.NewGuid();
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpaceSummary(spaceId, Guid.NewGuid(), "Vendor", true, 10, "IndividualVendor"));

        var handler = new CreateOwnedParkingAllocationHandler(_uow.Object, _parking.Object, _quota.Object);
        var result = await handler.HandleAsync(new CreateOwnedParkingAllocationCommand(
            company.Id, _adminId,
            new CreateOwnedParkingAllocationDto(spaceId, 5, 0, 5, 0m, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), null)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not owned");
    }

    [Fact]
    public async Task CreateOwned_WhenValid_Activates()
    {
        var company = Company.Create("Acme", "REG-OA2", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var spaceId = Guid.NewGuid();
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnedSpace(spaceId, company.Id, spots: 10));

        var handler = new CreateOwnedParkingAllocationHandler(_uow.Object, _parking.Object, _quota.Object);
        var result = await handler.HandleAsync(new CreateOwnedParkingAllocationCommand(
            company.Id, _adminId,
            new CreateOwnedParkingAllocationDto(
                spaceId, 5, 1, 4, 0m,
                DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(3), null)));

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(AllocationStatus.Active);
        result.Data.SourceType.Should().Be(ParkingAllocationSource.CompanyOwned);
        result.Data.ParkingSpaceId.Should().Be(spaceId);
    }

    [Fact]
    public async Task UpdatePolicy_WhenMissingAllocation_Fails()
    {
        var company = Company.Create("Acme", "REG-OA3", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var handler = new UpdateBookingPolicyHandler(_uow.Object, _parking.Object, _quota.Object);
        var result = await handler.HandleAsync(new UpdateBookingPolicyCommand(
            company.Id, Guid.NewGuid(), _adminId, new BookingPolicyDto(2, 10, 1, null, null, true)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Allocation not found");
    }

    [Fact]
    public async Task UpdatePolicy_WhenValid_Succeeds()
    {
        var company = Company.Create("Acme", "REG-OA4", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId, Quota.Create(5, 0, 5), 0m,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), parkingCapacity: 5);
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnedSpace(spaceId, company.Id));

        var handler = new UpdateBookingPolicyHandler(_uow.Object, _parking.Object, _quota.Object);
        var result = await handler.HandleAsync(new UpdateBookingPolicyCommand(
            company.Id, allocation.Id, _adminId,
            new BookingPolicyDto(2, 8, 2, TimeSpan.FromHours(8), TimeSpan.FromHours(20), true)));

        result.Success.Should().BeTrue();
        result.Data!.Policy!.MaxBookingsPerEmployeePerDay.Should().Be(2);
        result.Data.Policy.AllowWeekends.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateContract_WhenValid_UpdatesRate()
    {
        var company = Company.Create("Acme", "REG-OA5", "a@acme.com", "555", "Addr", BillingType.ReservedSlots, _adminId);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId, Quota.Create(5, 0, 5), 1000m,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(3), parkingCapacity: 5);
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnedSpace(spaceId, company.Id));

        var handler = new UpdateAllocationContractHandler(_uow.Object, _parking.Object, _users.Object, _quota.Object);
        var result = await handler.HandleAsync(new UpdateAllocationContractCommand(
            company.Id, allocation.Id, _adminId,
            new UpdateAllocationContractDto(1500m, allocation.StartDate, allocation.EndDate, "LEASE-42")));

        result.Success.Should().BeTrue();
        result.Data!.MonthlyRate.Should().Be(1500m);
        result.Data.LeaseReference.Should().Be("LEASE-42");
    }

    [Fact]
    public async Task AssignFixedSlot_WhenValid_Assigns()
    {
        var company = Company.Create("Acme", "REG-OA6", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(_adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId, Quota.Create(5, 2, 3), 0m,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), parkingCapacity: 5);
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnedSpace(spaceId, company.Id));

        var handler = new AssignFixedSlotHandler(_uow.Object, _parking.Object, _quota.Object, _cache.Object);
        var result = await handler.HandleAsync(new AssignFixedSlotCommand(
            company.Id, allocation.Id, _adminId, new AssignFixedSlotDto(membership.Id, 1)));

        result.Success.Should().BeTrue();
        result.Data!.FixedAssignments.Should().Contain(f => f.MembershipId == membership.Id && f.SlotNumber == 1);
    }

    [Fact]
    public async Task RemoveFixedSlot_WhenNotAdmin_Fails()
    {
        var company = Company.Create("Acme", "REG-OA7", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(_adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId, Quota.Create(5, 2, 3), 0m,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), parkingCapacity: 5);
        company.AssignFixedSlot(_adminId, allocation.Id, membership.Id, 1);
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var handler = new RemoveFixedSlotHandler(_uow.Object, _parking.Object, _quota.Object, _cache.Object);
        var result = await handler.HandleAsync(new RemoveFixedSlotCommand(
            company.Id, allocation.Id, employeeId, membership.Id));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("admins");
    }

    [Fact]
    public async Task RemoveFixedSlot_WhenAdmin_Removes()
    {
        var company = Company.Create("Acme", "REG-OA8", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(_adminId, employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId, Quota.Create(5, 2, 3), 0m,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), parkingCapacity: 5);
        company.AssignFixedSlot(_adminId, allocation.Id, membership.Id, 1);
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnedSpace(spaceId, company.Id));

        var handler = new RemoveFixedSlotHandler(_uow.Object, _parking.Object, _quota.Object, _cache.Object);
        var result = await handler.HandleAsync(new RemoveFixedSlotCommand(
            company.Id, allocation.Id, _adminId, membership.Id));

        result.Success.Should().BeTrue();
        result.Data!.FixedAssignments.Should().NotContain(f => f.MembershipId == membership.Id);
    }
}
