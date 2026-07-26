using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.Ancillary;
using ParkingApp.Marketplace.Application.Queries.Ancillary;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/ancillary-services")]
[Produces("application/json")]
public class AncillaryServicesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AncillaryServicesController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>Public/guest: catalog for a parking space (active only by default).</summary>
    [HttpGet("by-parking/{parkingSpaceId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<ParkingAncillaryServiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByParking(
        Guid parkingSpaceId,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new GetAncillaryServicesForParkingQuery(parkingSpaceId, activeOnly),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Vendor: all catalog rows for own listings (includes inactive).</summary>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<ParkingAncillaryServiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(
            new GetMyAncillaryServicesQuery(userId.Value),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ParkingAncillaryServiceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ParkingAncillaryServiceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateParkingAncillaryServiceDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new CreateParkingAncillaryServiceCommand(userId.Value, IsAdmin(), dto),
            cancellationToken);

        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);

        return CreatedAtAction(nameof(GetByParking), new { parkingSpaceId = dto.ParkingSpaceId }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ParkingAncillaryServiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateParkingAncillaryServiceDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new UpdateParkingAncillaryServiceCommand(id, userId.Value, IsAdmin(), dto),
            cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "Unauthorized") return Forbid();
            if (result.Message == "Add-on service not found") return NotFound(result);
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new DeactivateParkingAncillaryServiceCommand(id, userId.Value, IsAdmin()),
            cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "Unauthorized") return Forbid();
            if (result.Message == "Add-on service not found") return NotFound(result);
            return BadRequest(result);
        }

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool IsAdmin() =>
        User.IsInRole("Admin") || User.IsInRole("admin");
}
