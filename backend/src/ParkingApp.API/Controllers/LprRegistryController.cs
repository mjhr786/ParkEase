using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.Lpr;
using ParkingApp.Marketplace.Application.Queries.Lpr;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.API.Controllers;

/// <summary>
/// Vendor/admin facility LPR registry: camera API keys and plate allow/deny rules.
/// </summary>
[ApiController]
[Route("api/parking/{parkingSpaceId:guid}/lpr")]
[Authorize]
[Produces("application/json")]
public class LprRegistryController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public LprRegistryController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    // ── Camera keys ──────────────────────────────────────────────────────────

    [HttpGet("camera-keys")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LprCameraKeyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCameraKeys(Guid parkingSpaceId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var isAdmin))
            return Unauthorized();

        var result = await _dispatcher.QueryAsync(
            new GetLprCameraKeysQuery(parkingSpaceId, userId, isAdmin), cancellationToken);
        return result.Success ? Ok(result) : StatusCode(MapError(result.Message), result);
    }

    [HttpPost("camera-keys")]
    [ProducesResponseType(typeof(ApiResponse<LprCameraKeyCreatedDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCameraKey(
        Guid parkingSpaceId,
        [FromBody] CreateLprCameraKeyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var isAdmin))
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new CreateLprCameraKeyCommand(parkingSpaceId, userId, isAdmin, request.Name, request.KeyId),
            cancellationToken);

        if (!result.Success)
            return StatusCode(MapError(result.Message), result);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("camera-keys/{cameraKeyId:guid}/enabled")]
    public async Task<IActionResult> SetCameraKeyEnabled(
        Guid parkingSpaceId,
        Guid cameraKeyId,
        [FromBody] SetEnabledRequest body,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var isAdmin))
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new SetLprCameraKeyEnabledCommand(parkingSpaceId, cameraKeyId, userId, isAdmin, body.IsEnabled),
            cancellationToken);
        return result.Success ? Ok(result) : StatusCode(MapError(result.Message), result);
    }

    [HttpDelete("camera-keys/{cameraKeyId:guid}")]
    public async Task<IActionResult> DeleteCameraKey(
        Guid parkingSpaceId,
        Guid cameraKeyId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var isAdmin))
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new DeleteLprCameraKeyCommand(parkingSpaceId, cameraKeyId, userId, isAdmin),
            cancellationToken);
        return result.Success ? Ok(result) : StatusCode(MapError(result.Message), result);
    }

    // ── Plate rules ──────────────────────────────────────────────────────────

    [HttpGet("plate-rules")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LprPlateRuleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPlateRules(Guid parkingSpaceId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var isAdmin))
            return Unauthorized();

        var result = await _dispatcher.QueryAsync(
            new GetLprPlateRulesQuery(parkingSpaceId, userId, isAdmin), cancellationToken);
        return result.Success ? Ok(result) : StatusCode(MapError(result.Message), result);
    }

    [HttpPost("plate-rules")]
    [ProducesResponseType(typeof(ApiResponse<LprPlateRuleDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePlateRule(
        Guid parkingSpaceId,
        [FromBody] CreateLprPlateRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var isAdmin))
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new CreateLprPlateRuleCommand(
                parkingSpaceId, userId, isAdmin, request.LicensePlate, request.RuleType, request.Note),
            cancellationToken);

        if (!result.Success)
            return StatusCode(MapError(result.Message), result);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("plate-rules/{ruleId:guid}/enabled")]
    public async Task<IActionResult> SetPlateRuleEnabled(
        Guid parkingSpaceId,
        Guid ruleId,
        [FromBody] SetEnabledRequest body,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var isAdmin))
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new SetLprPlateRuleEnabledCommand(parkingSpaceId, ruleId, userId, isAdmin, body.IsEnabled),
            cancellationToken);
        return result.Success ? Ok(result) : StatusCode(MapError(result.Message), result);
    }

    [HttpDelete("plate-rules/{ruleId:guid}")]
    public async Task<IActionResult> DeletePlateRule(
        Guid parkingSpaceId,
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var userId, out var isAdmin))
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new DeleteLprPlateRuleCommand(parkingSpaceId, ruleId, userId, isAdmin),
            cancellationToken);
        return result.Success ? Ok(result) : StatusCode(MapError(result.Message), result);
    }

    private bool TryGetActor(out Guid userId, out bool isAdmin)
    {
        userId = Guid.Empty;
        isAdmin = User.IsInRole("Admin");
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }

    private static int MapError(string? message) =>
        string.Equals(message, "Unauthorized", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status403Forbidden
            : string.Equals(message, "Parking space not found", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

    public sealed record SetEnabledRequest(bool IsEnabled);
}
