using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.API.Filters;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.Lpr;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.API.Controllers;

/// <summary>
/// IoT LPR webhook + vendor/admin simulator for ticketless gate access.
/// </summary>
[ApiController]
[Route("api/iot")]
[Produces("application/json")]
public class IotLprController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public IotLprController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Camera / IoT webhook. Authenticate with X-Api-Key.
    /// Business denials return HTTP 200 with AccessGranted=false.
    /// </summary>
    [HttpPost("lpr-events")]
    [AllowAnonymous]
    [IotApiKey]
    [ProducesResponseType(typeof(ApiResponse<LprAccessResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessLprEvent(
        [FromBody] ProcessLprEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseDirection(request.Direction, out var direction))
        {
            return BadRequest(new ApiResponse<LprAccessResultDto>(
                false,
                "Direction must be Entry or Exit",
                null,
                new List<string> { "Invalid direction" }));
        }

        var keyId = HttpContext.Items[IotApiKeyAuthorizationFilter.KeyIdItemName] as string;
        var allowed = HttpContext.Items[IotApiKeyAuthorizationFilter.AllowedSpacesItemName] as IReadOnlyList<Guid>;

        var command = new ProcessLprAccessCommand(
            request.LicensePlate,
            request.ParkingSpaceId,
            direction,
            request.OccurredAtUtc,
            LprAccessSources.Iot,
            keyId,
            AllowedParkingSpaceIds: allowed,
            Confidence: request.Confidence,
            ImageUrl: request.ImageUrl,
            ImageBase64: request.ImageBase64);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        if (!result.Success && result.Data is null)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Simulator — Admin (any facility) or vendor (own facilities only).
    /// </summary>
    [HttpPost("lpr-events/simulate")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LprAccessResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SimulateLprEvent(
        [FromBody] ProcessLprEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseDirection(request.Direction, out var direction))
        {
            return BadRequest(new ApiResponse<LprAccessResultDto>(
                false,
                "Direction must be Entry or Exit",
                null,
                new List<string> { "Invalid direction" }));
        }

        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdRaw, out var userId))
            return Unauthorized();

        var isAdmin = User.IsInRole("Admin");

        var command = new ProcessLprAccessCommand(
            request.LicensePlate,
            request.ParkingSpaceId,
            direction,
            request.OccurredAtUtc,
            LprAccessSources.Simulator,
            ClientKeyId: isAdmin ? $"admin:{userId}" : $"vendor:{userId}",
            AllowedParkingSpaceIds: null,
            SimulatorUserId: userId,
            SimulatorIsAdmin: isAdmin,
            Confidence: request.Confidence,
            ImageUrl: request.ImageUrl,
            ImageBase64: request.ImageBase64);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        if (!result.Success && result.Data is null)
            return BadRequest(result);

        // Ownership denial as 403 for clearer UX
        if (result.Data is { AccessGranted: false, DenialReasonCode: LprDenialReasonCodes.NotFacilityOwner })
            return StatusCode(StatusCodes.Status403Forbidden, result);

        return Ok(result);
    }

    private static bool TryParseDirection(string? raw, out LprDirection direction)
    {
        direction = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return Enum.TryParse(raw.Trim(), ignoreCase: true, out direction)
               && Enum.IsDefined(direction);
    }
}
