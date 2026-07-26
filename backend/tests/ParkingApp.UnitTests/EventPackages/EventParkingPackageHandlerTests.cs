using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Marketplace.Application.Commands.EventPackages;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.UnitTests.EventPackages;

public class EventParkingPackageHandlerTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _uow = new();
    private readonly Mock<IParkingSpaceRepository> _spaces = new();
    private readonly Mock<IEventParkingPackageRepository> _packages = new();
    private readonly Guid _ownerId = Guid.NewGuid();

    public EventParkingPackageHandlerTests()
    {
        _uow.Setup(x => x.ParkingSpaces).Returns(_spaces.Object);
        _uow.Setup(x => x.EventParkingPackages).Returns(_packages.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _packages.Setup(x => x.AddAsync(It.IsAny<EventParkingPackage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventParkingPackage p, CancellationToken _) => p);
    }

    private ParkingSpace CreateSpace() =>
        ParkingSpace.CreateForVendor(
            _ownerId, "Event Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 50, 0, 0, 0, 0);

    [Fact]
    public async Task Create_WhenSpaceMissing_ReturnsNotFound()
    {
        _spaces.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpace?)null);

        var handler = new CreateEventParkingPackageHandler(_uow.Object, NullLogger<CreateEventParkingPackageHandler>.Instance);
        var dto = new CreateEventParkingPackageDto(
            Guid.NewGuid(), "Concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(3), 500m, 20);

        var result = await handler.HandleAsync(new CreateEventParkingPackageCommand(_ownerId, false, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
    }

    [Fact]
    public async Task Create_WhenNotOwner_ReturnsUnauthorized()
    {
        var space = CreateSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new CreateEventParkingPackageHandler(_uow.Object, NullLogger<CreateEventParkingPackageHandler>.Instance);
        var dto = new CreateEventParkingPackageDto(
            space.Id, "Concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(3), 500m, 20);

        var result = await handler.HandleAsync(new CreateEventParkingPackageCommand(Guid.NewGuid(), false, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task Create_WhenOwner_Succeeds()
    {
        var space = CreateSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _packages.Setup(x => x.GetByIdWithSpaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
            {
                var p = EventParkingPackage.Create(
                    space.Id, _ownerId, "Concert",
                    DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(3),
                    500m, 20);
                p.ParkingSpace = space;
                return p;
            });

        var handler = new CreateEventParkingPackageHandler(_uow.Object, NullLogger<CreateEventParkingPackageHandler>.Instance);
        var dto = new CreateEventParkingPackageDto(
            space.Id, "Concert", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(3), 500m, 20);

        var result = await handler.HandleAsync(new CreateEventParkingPackageCommand(_ownerId, false, dto));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        _packages.Verify(x => x.AddAsync(It.IsAny<EventParkingPackage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenMissing_ReturnsNotFound()
    {
        _packages.Setup(x => x.GetByIdWithSpaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventParkingPackage?)null);

        var handler = new UpdateEventParkingPackageHandler(_uow.Object);
        var result = await handler.HandleAsync(new UpdateEventParkingPackageCommand(
            Guid.NewGuid(), _ownerId, false, new UpdateEventParkingPackageDto(Title: "X")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Event package not found");
    }

    [Fact]
    public async Task Deactivate_WhenOwner_Succeeds()
    {
        var space = CreateSpace();
        var package = EventParkingPackage.Create(
            space.Id, _ownerId, "Concert",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(3),
            500m, 20);
        package.ParkingSpace = space;
        package.IsActive.Should().BeTrue();

        _packages.Setup(x => x.GetByIdWithSpaceAsync(package.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var handler = new DeactivateEventParkingPackageHandler(_uow.Object);
        var result = await handler.HandleAsync(new DeactivateEventParkingPackageCommand(package.Id, _ownerId, false));

        result.Success.Should().BeTrue();
        package.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_WhenNotOwner_ReturnsUnauthorized()
    {
        var space = CreateSpace();
        var package = EventParkingPackage.Create(
            space.Id, _ownerId, "Concert",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(3),
            500m, 20);
        package.ParkingSpace = space;

        _packages.Setup(x => x.GetByIdWithSpaceAsync(package.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var handler = new DeactivateEventParkingPackageHandler(_uow.Object);
        var result = await handler.HandleAsync(new DeactivateEventParkingPackageCommand(package.Id, Guid.NewGuid(), false));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }
}
