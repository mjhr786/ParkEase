using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using ParkingApp.API.Filters;
using ParkingApp.Marketplace.Application.Interfaces;

namespace ParkingApp.UnitTests.API;

public class IotApiKeyAuthorizationFilterTests
{
    private readonly Mock<ILprCameraKeyAuthenticator> _authenticator = new();
    private readonly IotApiKeyAuthorizationFilter _filter;

    public IotApiKeyAuthorizationFilterTests()
    {
        _filter = new IotApiKeyAuthorizationFilter(_authenticator.Object);
    }

    private static AuthorizationFilterContext CreateContext(string? apiKey)
    {
        var http = new DefaultHttpContext();
        if (apiKey is not null)
            http.Request.Headers[IotApiKeyAuthorizationFilter.HeaderName] = apiKey;

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public async Task OnAuthorization_MissingHeader_SetsUnauthorized()
    {
        var context = CreateContext(null);

        await _filter.OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<UnauthorizedObjectResult>();
        _authenticator.Verify(a => a.AuthenticateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnAuthorization_InvalidKey_SetsUnauthorized()
    {
        _authenticator
            .Setup(a => a.AuthenticateAsync("bad", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LprApiKeyAuthResult?)null);

        var context = CreateContext("bad");
        await _filter.OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task OnAuthorization_ValidKey_SetsItemsAndContinues()
    {
        var spaceId = Guid.NewGuid();
        _authenticator
            .Setup(a => a.AuthenticateAsync("good-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LprApiKeyAuthResult("key-1", new[] { spaceId }, true));

        var context = CreateContext("good-secret");
        await _filter.OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        context.HttpContext.Items[IotApiKeyAuthorizationFilter.KeyIdItemName].Should().Be("key-1");
        context.HttpContext.Items[IotApiKeyAuthorizationFilter.AllowedSpacesItemName]
            .Should().BeAssignableTo<IReadOnlyList<Guid>>()
            .Which.Should().Contain(spaceId);
    }
}
