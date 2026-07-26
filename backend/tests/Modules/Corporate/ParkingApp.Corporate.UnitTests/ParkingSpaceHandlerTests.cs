using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.ParkingSpaces;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Corporate.UnitTests;

public class ParkingSpaceHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICompanyOwnedParkingSpaceService> _parking = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<ICompanyQuotaCache> _quota = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public ParkingSpaceHandlerTests()
    {
        _uow.Setup(x => x.Companies).Returns(_companies.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _quota.Setup(x => x.InvalidateCompanyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static CreateParkingSpaceDto CreateDto() => new(
        Title: "HQ Lot",
        Description: "Company lot",
        Address: "1 Main",
        City: "Bengaluru",
        State: "KA",
        Country: "IN",
        PostalCode: "560001",
        Latitude: 12.9,
        Longitude: 77.6,
        ParkingType: ParkingType.Open,
        TotalSpots: 20,
        HourlyRate: 10,
        DailyRate: 100,
        WeeklyRate: 500,
        MonthlyRate: 2000,
        OpenTime: TimeSpan.FromHours(7),
        CloseTime: TimeSpan.FromHours(22));

    private static CompanyOwnedParkingSpaceDetail Detail(Guid companyId, Guid spaceId, bool active = true) =>
        new(spaceId, companyId, "HQ Lot", "Company lot", "1 Main", "Bengaluru", "KA", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 20, 20, 10, 100, 500, 2000,
            TimeSpan.FromHours(7), TimeSpan.FromHours(22), false,
            Array.Empty<string>(), Array.Empty<VehicleType>(), Array.Empty<string>(),
            active, true, null, null, DateTime.UtcNow, Guid.NewGuid());

    [Fact]
    public async Task Create_WhenNotAdmin_ReturnsFailure()
    {
        var company = Company.Create("Acme", "REG-PS1", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var employeeId = Guid.NewGuid();
        company.AddMember(_adminId, employeeId, CompanyRole.Employee);
        _companies.Setup(x => x.GetWithMembershipsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var handler = new CreateCorporateParkingSpaceHandler(_uow.Object, _parking.Object, _cache.Object, _quota.Object);
        var result = await handler.HandleAsync(new CreateCorporateParkingSpaceCommand(company.Id, employeeId, CreateDto()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("admins");
        _parking.Verify(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateParkingSpaceDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenAdmin_CreatesSpace()
    {
        var company = Company.Create("Acme", "REG-PS2", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var spaceId = Guid.NewGuid();
        _companies.Setup(x => x.GetWithMembershipsAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _parking.Setup(x => x.CreateAsync(company.Id, _adminId, It.IsAny<CreateParkingSpaceDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyOwnedParkingSpaceOpResult(true, "created", Detail(company.Id, spaceId)));

        var handler = new CreateCorporateParkingSpaceHandler(_uow.Object, _parking.Object, _cache.Object, _quota.Object);
        var result = await handler.HandleAsync(new CreateCorporateParkingSpaceCommand(company.Id, _adminId, CreateDto()));

        result.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(spaceId);
        result.Data.Title.Should().Be("HQ Lot");
        _quota.Verify(x => x.InvalidateCompanyAsync(company.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Toggle_WhenNotAdmin_ReturnsFailure()
    {
        _companies.Setup(x => x.GetMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCompanyMembership?)null);

        var handler = new ToggleCorporateParkingSpaceHandler(_uow.Object, _parking.Object, _cache.Object, _quota.Object);
        var result = await handler.HandleAsync(new ToggleCorporateParkingSpaceCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("admins");
    }

    [Fact]
    public async Task Toggle_WhenAdmin_Toggles()
    {
        var company = Company.Create("Acme", "REG-PS3", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var adminMembership = company.Memberships.First(m => m.UserId == _adminId);
        var spaceId = Guid.NewGuid();
        _companies.Setup(x => x.GetMembershipAsync(company.Id, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminMembership);
        _parking.Setup(x => x.ToggleActiveAsync(company.Id, spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyOwnedParkingSpaceOpResult(true, "Parking space deactivated.", Detail(company.Id, spaceId, active: false)));

        var handler = new ToggleCorporateParkingSpaceHandler(_uow.Object, _parking.Object, _cache.Object, _quota.Object);
        var result = await handler.HandleAsync(new ToggleCorporateParkingSpaceCommand(company.Id, _adminId, spaceId));

        result.Success.Should().BeTrue();
        result.Data!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Retire_WhenActiveAllocation_Blocks()
    {
        var company = Company.Create("Acme", "REG-PS4", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var adminMembership = company.Memberships.First(m => m.UserId == _adminId);
        var spaceId = Guid.NewGuid();
        company.CreateOwnedParkingAllocation(
            _adminId, spaceId, Quota.Create(5, 0, 5), 0m,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), parkingCapacity: 5);

        _companies.Setup(x => x.GetMembershipAsync(company.Id, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminMembership);
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var handler = new RetireCorporateParkingSpaceHandler(_uow.Object, _parking.Object, _cache.Object, _quota.Object);
        var result = await handler.HandleAsync(new RetireCorporateParkingSpaceCommand(company.Id, _adminId, spaceId));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("active allocations");
        _parking.Verify(x => x.RetireAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Retire_WhenNoAllocation_Succeeds()
    {
        var company = Company.Create("Acme", "REG-PS5", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var adminMembership = company.Memberships.First(m => m.UserId == _adminId);
        var spaceId = Guid.NewGuid();
        _companies.Setup(x => x.GetMembershipAsync(company.Id, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminMembership);
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _parking.Setup(x => x.RetireAsync(company.Id, spaceId, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyOwnedParkingSpaceOpResult(true, "retired", Detail(company.Id, spaceId, active: false)));

        var handler = new RetireCorporateParkingSpaceHandler(_uow.Object, _parking.Object, _cache.Object, _quota.Object);
        var result = await handler.HandleAsync(new RetireCorporateParkingSpaceCommand(company.Id, _adminId, spaceId));

        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WhenSpaceMissing_ReturnsFailure()
    {
        var company = Company.Create("Acme", "REG-PS6", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var adminMembership = company.Memberships.First(m => m.UserId == _adminId);
        _companies.Setup(x => x.GetMembershipAsync(company.Id, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminMembership);
        _parking.Setup(x => x.UpdateAsync(company.Id, It.IsAny<Guid>(), It.IsAny<CompanyOwnedParkingSpaceUpdate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyOwnedParkingSpaceOpResult(false, "Company-owned parking space not found."));

        var handler = new UpdateCorporateParkingSpaceHandler(_uow.Object, _parking.Object, _cache.Object, _quota.Object);
        var result = await handler.HandleAsync(new UpdateCorporateParkingSpaceCommand(
            company.Id, _adminId, Guid.NewGuid(),
            new UpdateCorporateParkingSpaceDto(
                "New", null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, null)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }
}
