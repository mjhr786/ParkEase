using FluentAssertions;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Application.Commands.ParkingPasses;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.UnitTests.ParkingPasses;

public class AssignCorporatePassHandlerTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _uow = new();
    private readonly Mock<IParkingSpaceRepository> _spaces = new();
    private readonly Mock<IParkingPassRepository> _passes = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public AssignCorporatePassHandlerTests()
    {
        _uow.Setup(x => x.ParkingSpaces).Returns(_spaces.Object);
        _uow.Setup(x => x.ParkingPasses).Returns(_passes.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _passes.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<ParkingPass>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Assign_WhenNotAdmin_ReturnsFailure()
    {
        _users.Setup(x => x.GetByIdAsync(_adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_adminId, "u@x.com", "U", "X", IsAdmin: false));

        var handler = new AssignCorporatePassHandler(_uow.Object, _users.Object, _cache.Object);
        var dto = new AssignCorporatePassDto(
            new[] { Guid.NewGuid() },
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            null, "ZONE-A",
            PassUsageMode.UnlimitedEntries,
            null, 20);

        var result = await handler.HandleAsync(new AssignCorporatePassCommand(_adminId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("administrators");
    }

    [Fact]
    public async Task Assign_WhenNoEmployees_ReturnsFailure()
    {
        _users.Setup(x => x.GetByIdAsync(_adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_adminId, "a@x.com", "A", "D", IsAdmin: true));

        var handler = new AssignCorporatePassHandler(_uow.Object, _users.Object, _cache.Object);
        var dto = new AssignCorporatePassDto(
            Array.Empty<Guid>(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            null, "ZONE-A",
            PassUsageMode.UnlimitedEntries,
            null, 10);

        var result = await handler.HandleAsync(new AssignCorporatePassCommand(_adminId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("At least one employee");
    }

    [Fact]
    public async Task Assign_WhenEmployeeMissing_ReturnsFailure()
    {
        var empId = Guid.NewGuid();
        _users.Setup(x => x.GetByIdAsync(_adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_adminId, "a@x.com", "A", "D", IsAdmin: true));
        _users.Setup(x => x.GetActiveByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserSummary>());

        var handler = new AssignCorporatePassHandler(_uow.Object, _users.Object, _cache.Object);
        var dto = new AssignCorporatePassDto(
            new[] { empId },
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            null, "ZONE-A",
            PassUsageMode.UnlimitedEntries,
            null, 10);

        var result = await handler.HandleAsync(new AssignCorporatePassCommand(_adminId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("inactive");
    }

    [Fact]
    public async Task Assign_WhenValidSpace_CreatesPasses()
    {
        var empId = Guid.NewGuid();
        _users.Setup(x => x.GetByIdAsync(_adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_adminId, "a@x.com", "A", "D", IsAdmin: true));
        _users.Setup(x => x.GetActiveByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserSummary>
            {
                new(empId, "e@x.com", "Emp", "One", IsActive: true)
            });

        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new AssignCorporatePassHandler(_uow.Object, _users.Object, _cache.Object);
        var dto = new AssignCorporatePassDto(
            new[] { empId },
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            space.Id, null,
            PassUsageMode.UnlimitedEntries,
            null, 25,
            "BATCH-1");

        var result = await handler.HandleAsync(new AssignCorporatePassCommand(_adminId, dto));

        result.Success.Should().BeTrue();
        result.Data!.CreatedCount.Should().Be(1);
        result.Data.CorporateBatchReference.Should().Be("BATCH-1");
        _passes.Verify(x => x.AddRangeAsync(It.IsAny<IEnumerable<ParkingPass>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
