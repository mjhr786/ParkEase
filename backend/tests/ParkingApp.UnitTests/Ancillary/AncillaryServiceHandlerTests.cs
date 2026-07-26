using FluentAssertions;
using Moq;
using ParkingApp.Marketplace.Application.Commands.Ancillary;
using ParkingApp.Marketplace.Application.Queries.Ancillary;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.UnitTests.Ancillary;

public class AncillaryServiceHandlerTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _uow = new();
    private readonly Mock<IParkingSpaceRepository> _spaces = new();
    private readonly Mock<IParkingAncillaryServiceRepository> _ancillary = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _spaceId = Guid.NewGuid();

    public AncillaryServiceHandlerTests()
    {
        _uow.Setup(x => x.ParkingSpaces).Returns(_spaces.Object);
        _uow.Setup(x => x.ParkingAncillaryServices).Returns(_ancillary.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _ancillary.Setup(x => x.AddAsync(It.IsAny<ParkingAncillaryService>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingAncillaryService s, CancellationToken _) => s);
    }

    private ParkingSpace CreateOwnedSpace(Guid? ownerId = null)
    {
        var space = ParkingSpace.CreateForVendor(
            ownerId ?? _ownerId,
            "Lot A", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        // Force id for stable assertions when needed
        typeof(ParkingSpace).GetProperty("Id")!.SetValue(space, _spaceId);
        return space;
    }

    [Fact]
    public async Task Create_WhenSpaceMissing_ReturnsNotFound()
    {
        _spaces.Setup(x => x.GetByIdAsync(_spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpace?)null);

        var handler = new CreateParkingAncillaryServiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new CreateParkingAncillaryServiceCommand(
            _ownerId, false, new CreateParkingAncillaryServiceDto(_spaceId, "Wash", 100m)));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
    }

    [Fact]
    public async Task Create_WhenNotOwner_ReturnsUnauthorized()
    {
        _spaces.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOwnedSpace());

        var handler = new CreateParkingAncillaryServiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new CreateParkingAncillaryServiceCommand(
            Guid.NewGuid(), false, new CreateParkingAncillaryServiceDto(_spaceId, "Wash", 100m)));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task Create_WhenOwner_Succeeds()
    {
        var space = CreateOwnedSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new CreateParkingAncillaryServiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new CreateParkingAncillaryServiceCommand(
            _ownerId, false, new CreateParkingAncillaryServiceDto(space.Id, "  Wash  ", 199.999m, "Exterior")));

        result.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("Wash");
        result.Data.Price.Should().Be(200.00m);
        _ancillary.Verify(x => x.AddAsync(It.IsAny<ParkingAncillaryService>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenAdmin_BypassesOwnership()
    {
        var space = CreateOwnedSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new CreateParkingAncillaryServiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new CreateParkingAncillaryServiceCommand(
            Guid.NewGuid(), true, new CreateParkingAncillaryServiceDto(space.Id, "Detail", 500m)));

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WhenServiceMissing_ReturnsNotFound()
    {
        _ancillary.Setup(x => x.GetByIdWithSpaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingAncillaryService?)null);

        var handler = new UpdateParkingAncillaryServiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new UpdateParkingAncillaryServiceCommand(
            Guid.NewGuid(), _ownerId, false, new UpdateParkingAncillaryServiceDto(Name: "X")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Add-on service not found");
    }

    [Fact]
    public async Task Update_WhenOwner_UpdatesPrice()
    {
        var space = CreateOwnedSpace();
        var service = ParkingAncillaryService.Create(space.Id, "Wash", 100m);
        service.ParkingSpace = space;
        _ancillary.Setup(x => x.GetByIdWithSpaceAsync(service.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        var handler = new UpdateParkingAncillaryServiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new UpdateParkingAncillaryServiceCommand(
            service.Id, _ownerId, false, new UpdateParkingAncillaryServiceDto(Price: 150m)));

        result.Success.Should().BeTrue();
        result.Data!.Price.Should().Be(150m);
        _ancillary.Verify(x => x.Update(service), Times.Once);
    }

    [Fact]
    public async Task Deactivate_WhenUnauthorized_ReturnsFailure()
    {
        var space = CreateOwnedSpace();
        var service = ParkingAncillaryService.Create(space.Id, "Wash", 100m);
        service.ParkingSpace = space;
        _ancillary.Setup(x => x.GetByIdWithSpaceAsync(service.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        var handler = new DeactivateParkingAncillaryServiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new DeactivateParkingAncillaryServiceCommand(
            service.Id, Guid.NewGuid(), false));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
        service.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_WhenOwner_Succeeds()
    {
        var space = CreateOwnedSpace();
        var service = ParkingAncillaryService.Create(space.Id, "Wash", 100m);
        service.ParkingSpace = space;
        _ancillary.Setup(x => x.GetByIdWithSpaceAsync(service.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        var handler = new DeactivateParkingAncillaryServiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new DeactivateParkingAncillaryServiceCommand(
            service.Id, _ownerId, false));

        result.Success.Should().BeTrue();
        service.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetByParking_MapsDtos()
    {
        var space = CreateOwnedSpace();
        var service = ParkingAncillaryService.Create(space.Id, "Wash", 100m);
        _ancillary.Setup(x => x.GetByParkingSpaceIdAsync(space.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkingAncillaryService> { service });

        var handler = new GetAncillaryServicesForParkingHandler(_uow.Object);
        var result = await handler.HandleAsync(new GetAncillaryServicesForParkingQuery(space.Id, true));

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].Name.Should().Be("Wash");
    }

    [Fact]
    public async Task GetMine_WhenNoSpaces_ReturnsEmpty()
    {
        _spaces.Setup(x => x.GetByOwnerIdAsync(_ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ParkingSpace>());

        var handler = new GetMyAncillaryServicesHandler(_uow.Object);
        var result = await handler.HandleAsync(new GetMyAncillaryServicesQuery(_ownerId));

        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
        _ancillary.Verify(x => x.GetByParkingSpaceIdsAsync(
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
