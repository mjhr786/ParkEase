using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.API.Filters;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.Lpr;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.UnitTests.API;

public class IotLprControllerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly IotLprController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _spaceId = Guid.NewGuid();

    public IotLprControllerTests()
    {
        _controller = new IotLprController(_dispatcher.Object);
        var http = new DefaultHttpContext();
        http.Items[IotApiKeyAuthorizationFilter.KeyIdItemName] = "cam-1";
        http.Items[IotApiKeyAuthorizationFilter.AllowedSpacesItemName] = new List<Guid> { _spaceId };
        _controller.ControllerContext = new ControllerContext { HttpContext = http };
    }

    private static LprAccessResultDto AccessDto(bool granted, string? denial = null) =>
        new(
            granted,
            granted ? "Granted" : "Denied",
            denial,
            denial,
            granted ? Guid.NewGuid() : null,
            granted ? "BK-1" : null,
            Guid.NewGuid(),
            "KA01AB1234",
            "Entry",
            DateTime.UtcNow,
            Guid.NewGuid());

    [Fact]
    public async Task ProcessLprEvent_InvalidDirection_ReturnsBadRequest()
    {
        var request = new ProcessLprEventRequest("KA01AB1234", _spaceId, "Sideways");

        var result = await _controller.ProcessLprEvent(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(It.IsAny<ProcessLprAccessCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessLprEvent_Valid_ReturnsOkWithDispatcherResult()
    {
        var dto = AccessDto(true);
        var response = new ApiResponse<LprAccessResultDto>(true, null, dto);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<ProcessLprAccessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var request = new ProcessLprEventRequest("KA01AB1234", _spaceId, "Entry", Confidence: 0.95);

        var result = await _controller.ProcessLprEvent(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<ProcessLprAccessCommand>(c =>
                c.ParkingSpaceId == _spaceId &&
                c.ClientKeyId == "cam-1" &&
                c.Source == LprAccessSources.Iot),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessLprEvent_WhenHandlerFailsWithoutData_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<ProcessLprAccessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<LprAccessResultDto>(false, "bad plate", null));

        var request = new ProcessLprEventRequest("???", _spaceId, "Entry");

        var result = await _controller.ProcessLprEvent(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SimulateLprEvent_WhenNotFacilityOwner_Returns403()
    {
        SetUser(_userId, isAdmin: false);
        var dto = AccessDto(false, LprDenialReasonCodes.NotFacilityOwner);
        var response = new ApiResponse<LprAccessResultDto>(true, null, dto);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<ProcessLprAccessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var request = new ProcessLprEventRequest("KA01AB1234", _spaceId, "Entry");
        var result = await _controller.SimulateLprEvent(request, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task SimulateLprEvent_WhenAdmin_ReturnsOk()
    {
        SetUser(_userId, isAdmin: true);
        var response = new ApiResponse<LprAccessResultDto>(true, null, AccessDto(true));
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<ProcessLprAccessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var request = new ProcessLprEventRequest("KA01AB1234", _spaceId, "Exit");
        var result = await _controller.SimulateLprEvent(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<ProcessLprAccessCommand>(c =>
                c.Source == LprAccessSources.Simulator &&
                c.SimulatorIsAdmin == true &&
                c.SimulatorUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SimulateLprEvent_WhenNoUser_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var request = new ProcessLprEventRequest("KA01AB1234", _spaceId, "Entry");
        var result = await _controller.SimulateLprEvent(request, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    private void SetUser(Guid userId, bool isAdmin)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"))
            }
        };
    }
}
