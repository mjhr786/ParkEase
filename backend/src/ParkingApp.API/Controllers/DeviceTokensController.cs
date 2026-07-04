using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.DeviceTokens;

namespace ParkingApp.API.Controllers;

[Authorize]
[ApiController]
[Route("api/device-tokens")]
[Produces("application/json")]
public class DeviceTokensController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public DeviceTokensController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Register or refresh the FCM device token for the authenticated user.
    /// Calling this again with the same deviceId will update the token (upsert).
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new RegisterDeviceTokenCommand(
            UserId: userId.Value,
            DeviceId: request.DeviceId,
            Platform: request.Platform,
            FcmToken: request.FcmToken,
            AppVersion: request.AppVersion
        );

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return result.Success ? Ok(new { success = true }) : BadRequest(result);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

// ── Request DTO ────────────────────────────────────────────────────────────

public sealed record RegisterDeviceTokenRequest(
    [Required] string DeviceId,
    [Required] string Platform,
    [Required] string FcmToken,
    string? AppVersion
);
