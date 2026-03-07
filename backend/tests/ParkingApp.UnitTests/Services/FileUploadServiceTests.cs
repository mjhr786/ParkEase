using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class FileUploadServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<IFileStorage> _mockFileStorage;
    private readonly FileUploadService _service;

    public FileUploadServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        _mockCache = new Mock<ICacheService>();
        _mockFileStorage = new Mock<IFileStorage>();

        _service = new FileUploadService(_mockUow.Object, _mockCache.Object, _mockFileStorage.Object);
    }

    [Fact]
    public async Task UploadParkingImagesAsync_ShouldThrow_WhenParkingNotFound()
    {
        var parkingId = Guid.NewGuid();
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace?)null);

        Func<Task> act = async () => await _service.UploadParkingImagesAsync(parkingId, Guid.NewGuid(), new List<(Stream, string, string)>());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UploadParkingImagesAsync_ShouldThrow_WhenNotOwner()
    {
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = Guid.NewGuid() };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        Func<Task> act = async () => await _service.UploadParkingImagesAsync(parkingId, Guid.NewGuid(), new List<(Stream, string, string)>());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UploadParkingImagesAsync_ShouldSucceed_WhenValid()
    {
        var parkingId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = ownerId, ImageUrls = "url1" };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var stream = new MemoryStream();
        _mockFileStorage.Setup(f => f.UploadFileAsync(stream, "file.jpg", "image/jpeg", It.IsAny<CancellationToken>())).ReturnsAsync("url2");

        var result = await _service.UploadParkingImagesAsync(parkingId, ownerId, new List<(Stream, string, string)> { (stream, "file.jpg", "image/jpeg") });

        result.Should().ContainSingle().Which.Should().Be("url2");
        parking.ImageUrls.Should().Be("url1,url2");
        _mockParkingRepo.Verify(r => r.Update(parking), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"parking:{parkingId}", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveByPatternAsync("search:*", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GeneratePresignedUrlAsync_ShouldThrow_WhenUnauthorized()
    {
        var parkingId = Guid.NewGuid();
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace?)null);

        Func<Task> act = async () => await _service.GeneratePresignedUrlAsync(parkingId, Guid.NewGuid(), "file.jpg", "image/jpeg");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GeneratePresignedUrlAsync_ShouldSucceed_WhenValid()
    {
        var parkingId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = ownerId };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        _mockFileStorage.Setup(f => f.GetPresignedUploadUrl("file.jpg", "image/jpeg", It.IsAny<TimeSpan>()))
            .Returns(("upload_url", "public_url", "key"));

        var result = await _service.GeneratePresignedUrlAsync(parkingId, ownerId, "file.jpg", "image/jpeg");

        result.UploadUrl.Should().Be("upload_url");
        result.PublicUrl.Should().Be("public_url");
        result.Key.Should().Be("key");
    }

    [Fact]
    public async Task ConfirmUploadAsync_ShouldUpdate_WhenValid()
    {
        var parkingId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = ownerId, ImageUrls = "url1" };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        await _service.ConfirmUploadAsync(parkingId, ownerId, new List<string> { "url2", "url3" });

        parking.ImageUrls.Should().Be("url1,url2,url3");
        _mockParkingRepo.Verify(r => r.Update(parking), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteParkingFileAsync_ShouldReturnFalse_WhenUnauthorized()
    {
        var result = await _service.DeleteParkingFileAsync(Guid.NewGuid(), Guid.NewGuid(), "url");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteParkingFileAsync_ShouldReturnFalse_WhenUrlNotFound()
    {
        var parkingId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = ownerId, ImageUrls = "url1,url2" };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var result = await _service.DeleteParkingFileAsync(parkingId, ownerId, "notfound");

        result.Should().BeFalse();
        _mockFileStorage.Verify(f => f.DeleteFileAsync("notfound", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteParkingFileAsync_ShouldSucceed_WhenValid()
    {
        var parkingId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = ownerId, ImageUrls = "url1,url2,url3" };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var result = await _service.DeleteParkingFileAsync(parkingId, ownerId, "url2");

        result.Should().BeTrue();
        parking.ImageUrls.Should().Be("url1,url3");
        _mockFileStorage.Verify(f => f.DeleteFileAsync("url2", It.IsAny<CancellationToken>()), Times.Once);
        _mockParkingRepo.Verify(r => r.Update(parking), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetParkingImagesAsync_ShouldReturnEmpty_WhenNoImages()
    {
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, ImageUrls = null };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var result = await _service.GetParkingImagesAsync(parkingId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetParkingImagesAsync_ShouldReturnList_WhenImagesExist()
    {
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, ImageUrls = "url1,url2" };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var result = await _service.GetParkingImagesAsync(parkingId);

        result.Should().BeEquivalentTo(new List<string> { "url1", "url2" });
    }
}
