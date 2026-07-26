using System.Text;
using FluentAssertions;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Application.Commands.FileUpload;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.UnitTests.FileUpload;

public class UploadParkingFilesHandlerTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _uow = new();
    private readonly Mock<IParkingSpaceRepository> _spaces = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly Guid _ownerId = Guid.NewGuid();

    public UploadParkingFilesHandlerTests()
    {
        _uow.Setup(x => x.ParkingSpaces).Returns(_spaces.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private ParkingSpace CreateSpace() =>
        ParkingSpace.CreateForVendor(
            _ownerId, "Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);

    [Fact]
    public async Task Upload_WhenNotOwner_ReturnsUnauthorized()
    {
        var space = CreateSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var handler = new UploadParkingFilesHandler(_uow.Object, _cache.Object, _storage.Object);
        var files = new List<UploadFilePayload>
        {
            new(new MemoryStream(Encoding.UTF8.GetBytes("img")), "a.jpg", "image/jpeg")
        };

        var result = await handler.HandleAsync(new UploadParkingFilesCommand(
            space.Id, Guid.NewGuid(), files));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unauthorized");
        _storage.Verify(x => x.UploadFileAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_WhenOwner_UploadsAndAppendsUrls()
    {
        var space = CreateSpace();
        _spaces.Setup(x => x.GetByIdAsync(space.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _storage.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), "a.jpg", "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn/a.jpg");
        _storage.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), "b.jpg", "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn/b.jpg");

        var handler = new UploadParkingFilesHandler(_uow.Object, _cache.Object, _storage.Object);
        var files = new List<UploadFilePayload>
        {
            new(new MemoryStream(Encoding.UTF8.GetBytes("1")), "a.jpg", "image/jpeg"),
            new(new MemoryStream(Encoding.UTF8.GetBytes("2")), "b.jpg", "image/jpeg")
        };

        var result = await handler.HandleAsync(new UploadParkingFilesCommand(space.Id, _ownerId, files));

        result.Success.Should().BeTrue();
        result.Data!.Urls.Should().HaveCount(2);
        _spaces.Verify(x => x.Update(space), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_WhenSpaceMissing_ReturnsUnauthorized()
    {
        _spaces.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpace?)null);

        var handler = new UploadParkingFilesHandler(_uow.Object, _cache.Object, _storage.Object);
        var result = await handler.HandleAsync(new UploadParkingFilesCommand(
            Guid.NewGuid(), _ownerId,
            new List<UploadFilePayload> { new(new MemoryStream(), "a.jpg", "image/jpeg") }));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Unauthorized");
    }
}
