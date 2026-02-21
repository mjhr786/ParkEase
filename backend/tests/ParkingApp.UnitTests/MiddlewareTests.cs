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
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        
        var mockLogger = new Mock<ILogger<RateLimitingMiddleware>>();
        RequestDelegate next = (innerContext) => Task.CompletedTask;
        var middleware = new RateLimitingMiddleware(next, mockLogger.Object);

        // Act: Hit the limit (Limit is 100 in middleware)
        for (int i = 0; i < 100; i++)
        {
            await middleware.InvokeAsync(context);
            context.Response.StatusCode.Should().Be(200);
        }

        // 101st request
        var context2 = new DefaultHttpContext();
        context2.Connection.RemoteIpAddress = IPAddress.Loopback;
        await middleware.InvokeAsync(context2);

        // Assert
        context2.Response.StatusCode.Should().Be(429);
    }
}
