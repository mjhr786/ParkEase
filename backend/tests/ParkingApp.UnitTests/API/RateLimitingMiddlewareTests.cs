using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ParkingApp.API.Middleware;

namespace ParkingApp.UnitTests.API;

public class RateLimitingMiddlewareTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/HEALTH")]
    [InlineData("/hubs/notifications")]
    [InlineData("/hubs/chat")]
    [InlineData("/assets/index-abc.js")]
    [InlineData("/uploads/parking/x.png")]
    [InlineData("/index.html")]
    [InlineData("/vite.svg")]
    [InlineData("/favicon.ico")]
    [InlineData("/")]
    [InlineData("/styles.css")]
    public void ShouldSkipRateLimit_StaticAndInfrastructurePaths(string path)
    {
        RateLimitingMiddleware.ShouldSkipRateLimit(new PathString(path)).Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/parking/search")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/bookings")]
    public void ShouldSkipRateLimit_ApiPaths_AreLimited(string path)
    {
        RateLimitingMiddleware.ShouldSkipRateLimit(new PathString(path)).Should().BeFalse();
    }
}
