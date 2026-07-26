using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ParkingApp.API.Middleware;
using System.Net;
using System.IO;
using System.Text;

namespace ParkingApp.UnitTests;

public class MiddlewareTests
{
    [Fact]
    public async Task SecurityHeadersMiddleware_ShouldAddSecurityHeaders()
    {
        // Arrange
        var context = new DefaultHttpContext();
        RequestDelegate next = (innerContext) => Task.CompletedTask;
        var middleware = new SecurityHeadersMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        context.Response.Headers["Content-Security-Policy"].ToString().Should().Contain("default-src 'self'");
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_OnArgumentException_ShouldReturnBadRequest()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        
        var mockLogger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = (innerContext) => throw new ArgumentException("Test error");
        
        var middleware = new ExceptionHandlingMiddleware(next, mockLogger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var content = await reader.ReadToEndAsync();
        content.Should().Contain("Test error");
    }

    [Fact]
    public async Task RateLimitingMiddleware_WhenLimitExceeded_ShouldReturn429()
    {
        // Unique IP so static windows from other tests do not interfere.
        var clientIp = IPAddress.Parse($"203.0.113.{Random.Shared.Next(1, 254)}");
        var mockLogger = new Mock<ILogger<RateLimitingMiddleware>>();
        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new RateLimitingMiddleware(next, mockLogger.Object);

        static DefaultHttpContext CreateCtx(IPAddress ip)
        {
            var ctx = new DefaultHttpContext();
            ctx.Connection.RemoteIpAddress = ip;
            // Empty path is skipped; use a real API path.
            ctx.Request.Path = "/api/bookings";
            ctx.Request.Method = HttpMethods.Get;
            return ctx;
        }

        // Act: Hit the general API limit (100 / min / IP)
        for (int i = 0; i < 100; i++)
        {
            var ctx = CreateCtx(clientIp);
            await middleware.InvokeAsync(ctx);
            ctx.Response.StatusCode.Should().Be(200);
        }

        var context2 = CreateCtx(clientIp);
        await middleware.InvokeAsync(context2);

        context2.Response.StatusCode.Should().Be(429);
    }

    [Fact]
    public void RateLimitingMiddleware_IsIotPath_DetectsIotRoutes()
    {
        RateLimitingMiddleware.IsIotPath(new PathString("/api/iot/lpr-events")).Should().BeTrue();
        RateLimitingMiddleware.IsIotPath(new PathString("/api/bookings")).Should().BeFalse();
    }
}





