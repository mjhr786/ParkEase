using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.API;

public class FileUploadControllerTests
{
    private readonly Mock<IFileUploadService> _fileUploadServiceMock;
    private readonly FileUploadController _controller;

    public FileUploadControllerTests()
    {
        _fileUploadServiceMock = new Mock<IFileUploadService>();
        _controller = new FileUploadController(_fileUploadServiceMock.Object);
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

        _fileUploadServiceMock.Setup(s => s.UploadParkingImagesAsync(spaceId, userId, It.IsAny<List<(Stream, string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "http://url.com/test.jpg" });

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

        _fileUploadServiceMock.Setup(s => s.DeleteParkingFileAsync(spaceId, userId, "test.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteParkingFile(spaceId, "test.jpg", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

     [Fact]
    public async Task DeleteParkingFile_ReturnsNotFound_WhenServiceReturnsFalse()
    {
         var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _fileUploadServiceMock.Setup(s => s.DeleteParkingFileAsync(spaceId, userId, "test.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteParkingFile(spaceId, "test.jpg", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetParkingFiles_ReturnsOk()
    {
         _fileUploadServiceMock.Setup(s => s.GetParkingImagesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "url" });

        var result = await _controller.GetParkingFiles(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GeneratePresignedUrl_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        var spaceId = Guid.NewGuid();
        
        var req = new GenerateUrlRequest { FileName = "test.jpg", ContentType = "image/jpeg" };

        _fileUploadServiceMock.Setup(s => s.GeneratePresignedUrlAsync(spaceId, userId, req.FileName, req.ContentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UploadUrl: "uploadUrl", PublicUrl: "publicUrl", Key: "key"));

        var result = await _controller.GeneratePresignedUrl(spaceId, req, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ConfirmUpload_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        var spaceId = Guid.NewGuid();

        var req = new ConfirmUploadRequest { FileUrls = new List<string> { "url1" } };

        _fileUploadServiceMock.Setup(s => s.ConfirmUploadAsync(spaceId, userId, req.FileUrls, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.ConfirmUpload(spaceId, req, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
