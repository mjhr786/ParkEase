using FluentAssertions;
using Moq;
using ParkingApp.Marketplace.Application.Commands.Lpr;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.UnitTests.Lpr;

public class LprRegistryHandlerTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _uow = new();
    private readonly Mock<IParkingSpaceRepository> _spaces = new();
    private readonly Mock<ILprCameraKeyRepository> _keys = new();
    private readonly Mock<ILprPlateRuleRepository> _rules = new();
    private readonly Guid _ownerId = Guid.NewGuid();

    public LprRegistryHandlerTests()
    {
        _uow.Setup(x => x.ParkingSpaces).Returns(_spaces.Object);
        _uow.Setup(x => x.LprCameraKeys).Returns(_keys.Object);
        _uow.Setup(x => x.LprPlateRules).Returns(_rules.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _keys.Setup(x => x.AddAsync(It.IsAny<LprCameraKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LprCameraKey k, CancellationToken _) => k);
        _rules.Setup(x => x.AddAsync(It.IsAny<LprPlateRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LprPlateRule r, CancellationToken _) => r);
        _keys.Setup(x => x.KeyIdExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private ParkingSpace CreateLprSpace()
    {
        var space = ParkingSpace.CreateForVendor(
            _ownerId, "LPR Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        space.SetLprEnabled(true);
        return space;
    }

    [Fact]
    public async Task CreateCameraKey_WhenSpaceMissing_ReturnsNotFound()
    {
        _spaces.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpace?)null);

        var handler = new CreateLprCameraKeyHandler(_uow.Object);
        var result = await handler.HandleAsync(new CreateLprCameraKeyCommand(
            Guid.NewGuid(), _ownerId, false, "Gate", null));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
    }

    [Fact]
    public async Task CreateCameraKey_WhenLprDisabled_ReturnsFailure()
    {
        var space = ParkingSpace.CreateForVendor(
            _ownerId, "Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new CreateLprCameraKeyHandler(_uow.Object);
        var result = await handler.HandleAsync(new CreateLprCameraKeyCommand(
            space.Id, _ownerId, false, "Gate", null));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Enable LPR");
    }

    [Fact]
    public async Task CreateCameraKey_WhenOwnerAndLprEnabled_ReturnsSecretOnce()
    {
        var space = CreateLprSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new CreateLprCameraKeyHandler(_uow.Object);
        var result = await handler.HandleAsync(new CreateLprCameraKeyCommand(
            space.Id, _ownerId, false, "Gate Cam", null));

        result.Success.Should().BeTrue();
        result.Data!.Secret.Should().NotBeNullOrWhiteSpace();
        result.Data.Name.Should().Be("Gate Cam");
        _keys.Verify(x => x.AddAsync(It.IsAny<LprCameraKey>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCameraKey_WhenNotOwner_ReturnsUnauthorized()
    {
        var space = CreateLprSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new CreateLprCameraKeyHandler(_uow.Object);
        var result = await handler.HandleAsync(new CreateLprCameraKeyCommand(
            space.Id, Guid.NewGuid(), false, "Gate", null));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task SetCameraKeyEnabled_WhenKeyOnOtherSpace_ReturnsNotFound()
    {
        var space = CreateLprSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var (key, _) = LprCameraKey.Create(Guid.NewGuid(), "Other", _ownerId);
        _keys.Setup(x => x.GetByIdAsync(key.Id, It.IsAny<CancellationToken>())).ReturnsAsync(key);

        var handler = new SetLprCameraKeyEnabledHandler(_uow.Object);
        var result = await handler.HandleAsync(new SetLprCameraKeyEnabledCommand(
            space.Id, key.Id, _ownerId, false, false));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Camera key not found");
    }

    [Fact]
    public async Task CreatePlateRule_WhenOwner_Succeeds()
    {
        var space = CreateLprSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _rules.Setup(x => x.ExistsAsync(space.Id, It.IsAny<string>(), LprPlateRuleType.Allow, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateLprPlateRuleHandler(_uow.Object);
        var result = await handler.HandleAsync(new CreateLprPlateRuleCommand(
            space.Id, _ownerId, false, "KA01AB1234", LprPlateRuleType.Allow, "VIP"));

        result.Success.Should().BeTrue();
        result.Data!.LicensePlateNormalized.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DeleteCameraKey_WhenOwner_Succeeds()
    {
        var space = CreateLprSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        var (key, _) = LprCameraKey.Create(space.Id, "Gate", _ownerId);
        _keys.Setup(x => x.GetByIdAsync(key.Id, It.IsAny<CancellationToken>())).ReturnsAsync(key);

        var handler = new DeleteLprCameraKeyHandler(_uow.Object);
        var result = await handler.HandleAsync(new DeleteLprCameraKeyCommand(
            space.Id, key.Id, _ownerId, false));

        result.Success.Should().BeTrue();
        _keys.Verify(x => x.Remove(key), Times.Once);
    }
}
