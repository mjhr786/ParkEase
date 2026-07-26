using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Waitlist;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.UnitTests;

public class WaitlistHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IWaitlistPromotionService> _promotion = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public WaitlistHandlerTests()
    {
        _uow.Setup(x => x.Companies).Returns(_companies.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private (Company company, CorporateWaitlistEntry entry, Guid employeeId) CreateCompanyWithWaitlist()
    {
        var company = Company.Create("Acme", "REG-W", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var employeeId = Guid.NewGuid();
        var membership = company.AddMember(_adminId, employeeId, CompanyRole.Employee);
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId,
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
            DateTime.UtcNow.AddHours(4),
            VehicleType.Car,
            null,
            priorityAtRequest: 1);
        company.WaitlistEntries.Add(entry);
        return (company, entry, employeeId);
    }

    [Fact]
    public async Task CancelWaitlist_WhenCompanyMissing_ReturnsFailure()
    {
        _companies.Setup(x => x.GetFullAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var handler = new CancelWaitlistEntryHandler(_uow.Object);
        var result = await handler.HandleAsync(new CancelWaitlistEntryCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task CancelWaitlist_WhenRequester_Succeeds()
    {
        var (company, entry, employeeId) = CreateCompanyWithWaitlist();
        _companies.Setup(x => x.GetFullAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var handler = new CancelWaitlistEntryHandler(_uow.Object);
        var result = await handler.HandleAsync(new CancelWaitlistEntryCommand(
            company.Id, employeeId, entry.Id));

        result.Success.Should().BeTrue();
        entry.Status.Should().Be(WaitlistStatus.Cancelled);
    }

    [Fact]
    public async Task PromoteWaitlist_DelegatesToService()
    {
        var companyId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var response = new ApiResponse<CorporateReservationResultDto>(true, "ok", null);
        _promotion.Setup(x => x.PromoteAsync(
                companyId, entryId, It.Is<Guid?>(id => id == _adminId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var handler = new PromoteWaitlistEntryHandler(_promotion.Object);
        var result = await handler.HandleAsync(new PromoteWaitlistEntryCommand(companyId, _adminId, entryId));

        result.Should().BeSameAs(response);
        _promotion.Verify(x => x.PromoteAsync(
            companyId, entryId, It.Is<Guid?>(id => id == _adminId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
