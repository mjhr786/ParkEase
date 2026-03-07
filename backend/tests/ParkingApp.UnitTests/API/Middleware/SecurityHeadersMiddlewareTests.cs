using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ParkingApp.API.Middleware;
using Xunit;

namespace ParkingApp.UnitTests.API.Middleware;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AddsSecurityHeaders()
    {
        // Arrange
        RequestDelegate next = (HttpContext hc) => Task.CompletedTask;
        var middleware = new SecurityHeadersMiddleware(next);
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var headers = context.Response.Headers;
        headers.Should().ContainKey("X-Frame-Options");
        headers["X-Frame-Options"].ToString().Should().Be("DENY");
        
        headers.Should().ContainKey("X-Content-Type-Options");
        headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        
        headers.Should().ContainKey("X-XSS-Protection");
        headers["X-XSS-Protection"].ToString().Should().Be("1; mode=block");
        
        headers.Should().ContainKey("Content-Security-Policy");
        headers["Content-Security-Policy"].ToString().Should().Contain("default-src 'self'");
        
        headers.Should().ContainKey("Referrer-Policy");
        headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        
        headers.Should().ContainKey("Permissions-Policy");
        headers["Permissions-Policy"].ToString().Should().Be("geolocation=(), microphone=(), camera=()");
    }
}
