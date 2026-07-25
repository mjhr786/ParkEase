using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.API.Middleware;
using ParkingApp.API.Options;
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

    private ImageResizingMiddleware CreateMiddleware(RequestDelegate next, bool enableRuntimeResize = true)
    {
        var options = new TestOptionsMonitor(new MediaOptions
        {
            EnableRuntimeResize = enableRuntimeResize,
            MaxRuntimeResizeDimension = 800
        });
        return new ImageResizingMiddleware(next, _loggerMock.Object, _envMock.Object, options);
    }

    [Fact]
    public async Task InvokeAsync_NotAnUploadPath_CallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = (HttpContext hc) => { nextCalled = true; return Task.CompletedTask; };
        var middleware = CreateMiddleware(next, enableRuntimeResize: true);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/users";

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_NoResizeQuery_CallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = (HttpContext hc) => { nextCalled = true; return Task.CompletedTask; };
        var middleware = CreateMiddleware(next, enableRuntimeResize: true);
        var context = new DefaultHttpContext();
        context.Request.Path = "/uploads/test.jpg";

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_UnsupportedExtension_CallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = (HttpContext hc) => { nextCalled = true; return Task.CompletedTask; };
        var middleware = CreateMiddleware(next, enableRuntimeResize: true);
        var context = new DefaultHttpContext();
        context.Request.Path = "/uploads/test.txt";
        context.Request.QueryString = new QueryString("?w=100");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenRuntimeResizeDisabled_AlwaysPassesThrough()
    {
        // Free-tier / R2 default: middleware must never block the pipeline.
        var nextCalled = false;
        RequestDelegate next = (HttpContext hc) => { nextCalled = true; return Task.CompletedTask; };
        var middleware = CreateMiddleware(next, enableRuntimeResize: false);
        var context = new DefaultHttpContext();
        context.Request.Path = "/uploads/parking/photo.jpg";
        context.Request.QueryString = new QueryString("?w=200");

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<MediaOptions>
    {
        public TestOptionsMonitor(MediaOptions current) => CurrentValue = current;
        public MediaOptions CurrentValue { get; }
        public MediaOptions Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<MediaOptions, string?> listener) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
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
