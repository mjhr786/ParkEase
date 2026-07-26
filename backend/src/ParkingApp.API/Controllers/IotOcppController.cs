using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.API.Filters;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.EvCharging;
using ParkingApp.Marketplace.Application.Queries.EvCharging;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.API.Controllers;

/// <summary>
/// OCPP-inspired IoT charge pipeline + vendor/admin simulator (mock station first).
/// </summary>
[ApiController]
[Route("api/iot/ocpp")]
[Produces("application/json")]
public class IotOcppController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public IotOcppController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>Start charge transaction (station / CSMS webhook).</summary>
    [HttpPost("start-transaction")]
    [AllowAnonymous]
    [IotApiKey]
    [ProducesResponseType(typeof(ApiResponse<EvChargingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartTransaction(
        [FromBody] StartEvChargingTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new StartEvChargingSessionCommand(
                request.BookingId,
                request.StationId,
                request.ConnectorId,
                request.MeterStartKwh,
                EvChargingSources.Iot),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Record meter values while charging.</summary>
    [HttpPost("meter-values")]
    [AllowAnonymous]
    [IotApiKey]
    [ProducesResponseType(typeof(ApiResponse<EvChargingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MeterValues(
        [FromBody] EvMeterValuesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new RecordEvMeterValuesCommand(request.OcppTransactionId, request.MeterKwh, EvChargingSources.Iot),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Stop charge transaction and settle energy fee (PerKwh).</summary>
    [HttpPost("stop-transaction")]
    [AllowAnonymous]
    [IotApiKey]
    [ProducesResponseType(typeof(ApiResponse<EvChargingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StopTransaction(
        [FromBody] StopEvChargingTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new StopEvChargingSessionCommand(
                request.OcppTransactionId,
                request.MeterStopKwh,
                EvChargingSources.Iot),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Simulator — Admin or vendor (own facilities via booking ownership).</summary>
    [HttpPost("simulate")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<EvChargingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Simulate(
        [FromBody] SimulateEvChargingSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var isAdmin = User.IsInRole("Admin");
        var result = await _dispatcher.SendAsync(
            new SimulateEvChargingSessionCommand(
                request.BookingId,
                request.EnergyKwh,
                request.StationId,
                request.ConnectorId,
                userId,
                isAdmin),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }
}

/// <summary>Guest/vendor EV session read on bookings controller path also available here for IoT clients.</summary>
[ApiController]
[Route("api/bookings")]
[Authorize]
[Produces("application/json")]
public class BookingEvSessionController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public BookingEvSessionController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet("{id:guid}/ev-session")]
    [ProducesResponseType(typeof(ApiResponse<EvChargingSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvSession(Guid id, CancellationToken cancellationToken)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId))
            return Unauthorized();

        var result = await _dispatcher.QueryAsync(
            new GetEvChargingSessionByBookingQuery(id, userId, User.IsInRole("Admin")),
            cancellationToken);

        if (!result.Success && string.Equals(result.Message, "Unauthorized", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, result);

        return result.Success ? Ok(result) : NotFound(result);
    }
}
