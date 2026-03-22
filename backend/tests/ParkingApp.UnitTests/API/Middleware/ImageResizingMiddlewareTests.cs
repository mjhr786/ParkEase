using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.API.Middleware;
using Xunit;

namespace ParkingApp.UnitTests.API.Middleware;

public class ImageResizingMiddlewareTests
{
    private readonly Mock<ILogger<ImageResizingMiddleware>> _loggerMock;
    private readonly Mock<IWebHostEnvironment> _envMock;
    private readonly string _webRootPath;

    public ImageResizingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ImageResizingMiddleware>>();
        _envMock = new Mock<IWebHostEnvironment>();
        
        // Setup a temporary directory for tests
        _webRootPath = Path.Combine(Path.GetTempPath(), "ParkEase_Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_webRootPath);
        
        _envMock.Setup(e => e.WebRootPath).Returns(_webRootPath);
    }

    [Fact]
    public async Task InvokeAsync_NotAnUploadPath_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = (HttpContext hc) => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new ImageResizingMiddleware(next, _loggerMock.Object, _envMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/users";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_NoResizeQuery_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = (HttpContext hc) => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new ImageResizingMiddleware(next, _loggerMock.Object, _envMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Path = "/uploads/test.jpg";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_UnsupportedExtension_CallsNext()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = (HttpContext hc) => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new ImageResizingMiddleware(next, _loggerMock.Object, _envMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Path = "/uploads/test.txt";
        context.Request.QueryString = new QueryString("?w=100");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    // Cleanup the temporary test directory
    ~ImageResizingMiddlewareTests()
    {
        try
        {
            if (Directory.Exists(_webRootPath))
            {
                Directory.Delete(_webRootPath, true);
            }
        }
        catch { }
    }
}
