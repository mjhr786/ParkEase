using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.FileUpload;
using ParkingApp.Application.DTOs;
using Xunit;

namespace ParkingApp.UnitTests.API;

public class FileUploadControllerTests
{
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly FileUploadController _controller;

    public FileUploadControllerTests()
    {
        _dispatcherMock = new Mock<IDispatcher>();
        _controller = new FileUploadController(_dispatcherMock.Object);
    }

    private void SetupControllerUser(ControllerBase controller, Guid userId, string role = "Vendor")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task UploadParkingFiles_ReturnsOk_WhenValidImages()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.FileName).Returns("test.jpg");
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        var files = new List<IFormFile> { fileMock.Object };

        _dispatcherMock
            .Setup(d => d.SendAsync(It.IsAny<UploadParkingFilesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<UploadParkingFilesResultDto>(
                true,
                "1 file(s) uploaded successfully",
                new UploadParkingFilesResultDto(new List<string> { "http://url.com/test.jpg" }, new List<string>())));

        var result = await _controller.UploadParkingFiles(spaceId, files, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UploadParkingFiles_ReturnsBadRequest_WhenNoFiles()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        var result = await _controller.UploadParkingFiles(Guid.NewGuid(), new List<IFormFile>(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteParkingFile_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _dispatcherMock
            .Setup(d => d.SendAsync(It.IsAny<DeleteParkingFileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, "File deleted successfully", true));

        var result = await _controller.DeleteParkingFile(spaceId, "test.jpg", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetParkingFiles_ReturnsOk()
    {
        _dispatcherMock
            .Setup(d => d.QueryAsync(It.IsAny<GetParkingFilesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<List<string>>(true, null, new List<string> { "http://url.com/a.jpg" }));

        var result = await _controller.GetParkingFiles(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GeneratePresignedUrl_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _dispatcherMock
            .Setup(d => d.SendAsync(It.IsAny<GeneratePresignedUrlCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<PresignedUploadUrlDto>(
                true,
                null,
                new PresignedUploadUrlDto("https://upload", "https://public", "key")));

        var result = await _controller.GeneratePresignedUrl(
            Guid.NewGuid(),
            new GenerateUrlRequest { FileName = "test.jpg", ContentType = "image/jpeg" },
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ConfirmUpload_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _dispatcherMock
            .Setup(d => d.SendAsync(It.IsAny<ConfirmParkingUploadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, "Upload confirmed", true));

        var result = await _controller.ConfirmUpload(
            Guid.NewGuid(),
            new ConfirmUploadRequest { FileUrls = new List<string> { "https://public/test.jpg" } },
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}