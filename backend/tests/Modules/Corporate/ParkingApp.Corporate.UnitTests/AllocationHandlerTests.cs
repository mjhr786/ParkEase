using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Allocations;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Corporate.UnitTests;

public class AllocationHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IParkingSpaceLookup> _parking = new();
    private readonly Mock<ICompanyQuotaCache> _quota = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public AllocationHandlerTests()
    {
        _uow.Setup(x => x.Companies).Returns(_companies.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _quota.Setup(x => x.InvalidateCompanyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static ParkingSpaceSummary Space(Guid id, Guid ownerId, int spots = 20, bool companyOwned = false) =>
        new(id, ownerId, "Lot A", true, spots, companyOwned ? "CompanyOwned" : "IndividualVendor");

    [Fact]
    public async Task Allocate_WhenCompanyMissing_ReturnsNotFound()
    {
        _companies.Setup(x => x.GetWithAllocationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var handler = new AllocateParkingSlotsHandler(_uow.Object, _parking.Object, _quota.Object);
        var result = await handler.HandleAsync(new AllocateParkingSlotsCommand(
            Guid.NewGuid(), _adminId,
            new AllocateParkingSlotsDto(Guid.NewGuid(), 5, 1, 4, 1000m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(6), null, null)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Company not found");
    }

    [Fact]
    public async Task Allocate_WhenSpaceInactive_ReturnsFailure()
    {
        var company = Company.Create("Acme", "REG-AL", "a@acme.com", "555", "Addr", BillingType.ReservedSlots, _adminId);
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpaceSummary?)null);

        var handler = new AllocateParkingSlotsHandler(_uow.Object, _parking.Object, _quota.Object);
        var result = await handler.HandleAsync(new AllocateParkingSlotsCommand(
            company.Id, _adminId,
            new AllocateParkingSlotsDto(Guid.NewGuid(), 5, 0, 5, 500m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(3), "L-1", null)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Parking space");
    }

    [Fact]
    public async Task Allocate_WhenValidVendorLot_CreatesPending()
    {
        var company = Company.Create("Acme", "REG-AL2", "a@acme.com", "555", "Addr", BillingType.ReservedSlots, _adminId);
        var spaceId = Guid.NewGuid();
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Space(spaceId, _ownerId, spots: 20));

        var handler = new AllocateParkingSlotsHandler(_uow.Object, _parking.Object, _quota.Object);
        var result = await handler.HandleAsync(new AllocateParkingSlotsCommand(
            company.Id, _adminId,
            new AllocateParkingSlotsDto(
                spaceId, 5, 1, 4, 2500m,
                DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(6),
                "LEASE-9", null)));

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(AllocationStatus.PendingApproval);
        result.Data.ParkingSpaceId.Should().Be(spaceId);
        result.Data.VendorId.Should().Be(_ownerId);
        _quota.Verify(x => x.InvalidateCompanyAsync(company.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Approve_WhenOwner_Activates()
    {
        var company = Company.Create("Acme", "REG-AP", "a@acme.com", "555", "Addr", BillingType.ReservedSlots, _adminId);
        var spaceId = Guid.NewGuid();
        var allocation = company.RequestAllocation(
            _adminId, spaceId, Quota.Create(3, 0, 3), 1000m,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(3), parkingCapacity: 10);
        allocation.SetVendorLeaseMetadata(_ownerId, "L-1");

        _companies.Setup(x => x.GetAggregateByAllocationAsync(allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Space(spaceId, _ownerId));

        var handler = new ApproveAllocationHandler(_uow.Object, _parking.Object, _quota.Object);
        var result = await handler.HandleAsync(new ApproveAllocationCommand(allocation.Id, _ownerId));

        result.Success.Should().BeTrue();
        allocation.Status.Should().Be(AllocationStatus.Active);
    }

    [Fact]
    public async Task Approve_WhenNotOwner_ReturnsFailure()
    {
        var company = Company.Create("Acme", "REG-AP2", "a@acme.com", "555", "Addr", BillingType.ReservedSlots, _adminId);
        var spaceId = Guid.NewGuid();
        var allocation = company.RequestAllocation(
            _adminId, spaceId, Quota.Create(2, 0, 2), 500m,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), parkingCapacity: 5);
        allocation.SetVendorLeaseMetadata(_ownerId, null);

        _companies.Setup(x => x.GetAggregateByAllocationAsync(allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Space(spaceId, _ownerId));

        var handler = new ApproveAllocationHandler(_uow.Object, _parking.Object, _quota.Object);
        var result = await handler.HandleAsync(new ApproveAllocationCommand(allocation.Id, Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("owner");
    }

    [Fact]
    public async Task Reject_WhenOwner_Rejects()
    {
        var company = Company.Create("Acme", "REG-RJ", "a@acme.com", "555", "Addr", BillingType.ReservedSlots, _adminId);
        var spaceId = Guid.NewGuid();
        var allocation = company.RequestAllocation(
            _adminId, spaceId, Quota.Create(2, 0, 2), 500m,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), parkingCapacity: 5);
        allocation.SetVendorLeaseMetadata(_ownerId, null);

        _companies.Setup(x => x.GetAggregateByAllocationAsync(allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Space(spaceId, _ownerId));

        var handler = new RejectAllocationHandler(_uow.Object, _parking.Object, _quota.Object);
        var result = await handler.HandleAsync(new RejectAllocationCommand(allocation.Id, _ownerId, "No capacity"));

        result.Success.Should().BeTrue();
        allocation.Status.Should().Be(AllocationStatus.Rejected);
        allocation.RejectionReason.Should().Be("No capacity");
    }
}
