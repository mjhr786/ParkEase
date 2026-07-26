using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.EvCharging;
using ParkingApp.Marketplace.Application.Queries.EvCharging;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.UnitTests.API;

public class IotOcppControllerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly IotOcppController _controller;
    private readonly BookingEvSessionController _evSessionController;
    private readonly Guid _userId = Guid.NewGuid();

    public IotOcppControllerTests()
    {
        _controller = new IotOcppController(_dispatcher.Object);
        _evSessionController = new BookingEvSessionController(_dispatcher.Object);
        SetUser(_controller, _userId, isAdmin: false);
        SetUser(_evSessionController, _userId, isAdmin: false);
    }

    private static void SetUser(ControllerBase controller, Guid? userId, bool isAdmin)
    {
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"))
            }
        };
    }

    private static EvChargingSessionDto SampleSession() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "station-1",
            1,
            "tx-1",
            EvChargingSessionStatus.Charging,
            DateTime.UtcNow,
            null,
            0,
            5,
            null,
            5,
            10m,
            50m,
            EvChargingSources.Iot);

    [Fact]
    public async Task StartTransaction_WhenSuccess_ReturnsOk()
    {
        var response = new ApiResponse<EvChargingSessionDto>(true, null, SampleSession());
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<StartEvChargingSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var request = new StartEvChargingTransactionRequest(Guid.NewGuid(), "station-1", 1, 0);
        var result = await _controller.StartTransaction(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task MeterValues_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<RecordEvMeterValuesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EvChargingSessionDto>(false, "not found", null));

        var result = await _controller.MeterValues(
            new EvMeterValuesRequest("tx-1", 3.2m),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task StopTransaction_WhenSuccess_ReturnsOk()
    {
        var response = new ApiResponse<EvChargingSessionDto>(true, null, SampleSession());
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<StopEvChargingSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.StopTransaction(
            new StopEvChargingTransactionRequest("tx-1", 12.5m),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Simulate_WhenNoUser_ReturnsUnauthorized()
    {
        SetUser(_controller, null, isAdmin: false);

        var result = await _controller.Simulate(
            new SimulateEvChargingSessionRequest(Guid.NewGuid(), 10m, "s1", 1),
            CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Simulate_WhenAdmin_ReturnsOk()
    {
        SetUser(_controller, _userId, isAdmin: true);
        var response = new ApiResponse<EvChargingSessionDto>(true, null, SampleSession());
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<SimulateEvChargingSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.Simulate(
            new SimulateEvChargingSessionRequest(Guid.NewGuid(), 8m, "s1", 1),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<SimulateEvChargingSessionCommand>(c => c.ActorIsAdmin && c.ActorUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEvSession_WhenUnauthorizedMessage_Returns403()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetEvChargingSessionByBookingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EvChargingSessionDto>(false, "Unauthorized", null));

        var result = await _evSessionController.GetEvSession(Guid.NewGuid(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetEvSession_WhenNotFound_ReturnsNotFound()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetEvChargingSessionByBookingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EvChargingSessionDto>(false, "Not found", null));

        var result = await _evSessionController.GetEvSession(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
