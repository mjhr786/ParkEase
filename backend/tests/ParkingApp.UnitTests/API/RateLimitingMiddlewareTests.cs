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
    [InlineData("/api/payments/webhooks/razorpay")]
    [InlineData("/API/payments/webhooks/razorpay")]
    [InlineData("/api/payments/webhooks/stripe")]
    public void ShouldSkipRateLimit_StaticAndInfrastructurePaths(string path)
    {
        RateLimitingMiddleware.ShouldSkipRateLimit(new PathString(path)).Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/parking/search")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/bookings")]
    [InlineData("/api/payments/upi/create-order")]
    public void ShouldSkipRateLimit_ApiPaths_AreLimited(string path)
    {
        RateLimitingMiddleware.ShouldSkipRateLimit(new PathString(path)).Should().BeFalse();
    }

    [Theory]
    [InlineData("/api/auth/external")]
    [InlineData("/api/auth/external/link")]
    [InlineData("/api/auth/external/providers")]
    [InlineData("/API/auth/external")]
    public void IsExternalAuthPath_MatchesExternalRoutes(string path)
    {
        RateLimitingMiddleware.IsExternalAuthPath(new PathString(path)).Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/register")]
    [InlineData("/api/iot/lpr")]
    public void IsExternalAuthPath_DoesNotMatchOtherRoutes(string path)
    {
        RateLimitingMiddleware.IsExternalAuthPath(new PathString(path)).Should().BeFalse();
    }

    [Fact]
    public void IsUpiCreateOrderPath_MatchesCreateOrderOnly()
    {
        RateLimitingMiddleware.IsUpiCreateOrderPath(new PathString("/api/payments/upi/create-order")).Should().BeTrue();
        RateLimitingMiddleware.IsUpiCreateOrderPath(new PathString("/API/payments/upi/create-order")).Should().BeTrue();
        RateLimitingMiddleware.IsUpiCreateOrderPath(new PathString("/api/payments/upi/orders/order_1")).Should().BeFalse();
        RateLimitingMiddleware.IsUpiCreateOrderPath(new PathString("/api/payments/create-order")).Should().BeFalse();
    }
}
