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

public class ParkingPassHandlerTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _uow = new();
    private readonly Mock<IParkingSpaceRepository> _spaces = new();
    private readonly Mock<IParkingPassRepository> _passes = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Guid _userId = Guid.NewGuid();

    public ParkingPassHandlerTests()
    {
        _uow.Setup(x => x.ParkingSpaces).Returns(_spaces.Object);
        _uow.Setup(x => x.ParkingPasses).Returns(_passes.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _passes.Setup(x => x.AddAsync(It.IsAny<ParkingPass>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingPass p, CancellationToken _) => p);
        _passes.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => null);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Create_WhenCorporateType_ReturnsFailure()
    {
        var handler = new CreateParkingPassHandler(_uow.Object, _users.Object, _cache.Object);
        var dto = new CreateParkingPassDto(
            PassTypeKind.Corporate,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            null, null,
            PassUsageMode.UnlimitedEntries,
            null, 10);

        var result = await handler.HandleAsync(new CreateParkingPassCommand(_userId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("corporate endpoint");
    }

    [Fact]
    public async Task Create_WhenUserInactive_ReturnsFailure()
    {
        _users.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "a@b.com", "A", "B", IsActive: false));

        var handler = new CreateParkingPassHandler(_uow.Object, _users.Object, _cache.Object);
        var dto = new CreateParkingPassDto(
            PassTypeKind.Monthly,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            null, "ZONE-A",
            PassUsageMode.UnlimitedEntries,
            null, 10);

        var result = await handler.HandleAsync(new CreateParkingPassCommand(_userId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not active");
    }

    [Fact]
    public async Task Create_WhenSpaceInactive_ReturnsFailure()
    {
        _users.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "a@b.com", "A", "B"));

        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        space.IsActive = false;
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new CreateParkingPassHandler(_uow.Object, _users.Object, _cache.Object);
        var dto = new CreateParkingPassDto(
            PassTypeKind.Monthly,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            space.Id, null,
            PassUsageMode.UnlimitedEntries,
            null, 15);

        var result = await handler.HandleAsync(new CreateParkingPassCommand(_userId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not active");
    }

    [Fact]
    public async Task Create_WhenActiveSpace_Succeeds()
    {
        _users.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "a@b.com", "A", "B"));

        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new CreateParkingPassHandler(_uow.Object, _users.Object, _cache.Object);
        var dto = new CreateParkingPassDto(
            PassTypeKind.Monthly,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            space.Id, null,
            PassUsageMode.UnlimitedEntries,
            null, 20);

        var result = await handler.HandleAsync(new CreateParkingPassCommand(_userId, dto));

        result.Success.Should().BeTrue();
        _passes.Verify(x => x.AddAsync(It.IsAny<ParkingPass>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenZoneMissing_ReturnsFailure()
    {
        _users.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "a@b.com", "A", "B"));
        _spaces.Setup(x => x.ExistsWithZoneCodeAsync("ZONE-X", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateParkingPassHandler(_uow.Object, _users.Object, _cache.Object);
        var dto = new CreateParkingPassDto(
            PassTypeKind.Weekly,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            null, "zone-x",
            PassUsageMode.UnlimitedEntries,
            null, 5);

        var result = await handler.HandleAsync(new CreateParkingPassCommand(_userId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("zone does not exist");
    }
}
