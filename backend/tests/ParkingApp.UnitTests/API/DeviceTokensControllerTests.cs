using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.Commands.DeviceTokens;

namespace ParkingApp.UnitTests.API;

public class DeviceTokensControllerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly DeviceTokensController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public DeviceTokensControllerTests()
    {
        _controller = new DeviceTokensController(_dispatcher.Object);
        SetUser(_userId);
    }

    private void SetUser(Guid? userId)
    {
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"))
            }
        };
    }

    [Fact]
    public async Task Register_WhenNoUser_ReturnsUnauthorized()
    {
        SetUser(null);

        var result = await _controller.Register(
            new RegisterDeviceTokenRequest("dev-1", "android", "token", "1.0"),
            CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Register_WhenSuccess_ReturnsOk()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<RegisterDeviceTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, null, true));

        var result = await _controller.Register(
            new RegisterDeviceTokenRequest("dev-1", "ios", "fcm-token", "2.0"),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<RegisterDeviceTokenCommand>(c => c.UserId == _userId && c.DeviceId == "dev-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<RegisterDeviceTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(false, "invalid", false));

        var result = await _controller.Register(
            new RegisterDeviceTokenRequest("dev-1", "web", "x", null),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
