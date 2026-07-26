using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.EventPackages;
using ParkingApp.Marketplace.Application.Queries.EventPackages;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/event-packages")]
[Produces("application/json")]
public class EventPackagesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public EventPackagesController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>Public: packages currently on sale across facilities.</summary>
    [HttpGet("on-sale")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<EventParkingPackageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOnSale([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(new GetOnSaleEventPackagesQuery(take), cancellationToken);
        return Ok(result);
    }

    /// <summary>Public: on-sale packages grouped by venue event (multi-lot zones).</summary>
    [HttpGet("venues/on-sale")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<EventVenueOnSaleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVenuesOnSale([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(new GetOnSaleEventVenuesQuery(take), cancellationToken);
        return Ok(result);
    }

    /// <summary>Public: all packages/zones for a venue event.</summary>
    [HttpGet("by-venue-event/{venueEventId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<EventParkingPackageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByVenueEvent(
        Guid venueEventId,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new GetEventPackagesByVenueEventQuery(venueEventId, activeOnly),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Public: packages for a parking space.</summary>
    [HttpGet("by-parking/{parkingSpaceId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<EventParkingPackageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByParking(
        Guid parkingSpaceId,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new GetEventPackagesForParkingQuery(parkingSpaceId, activeOnly),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Vendor: sell-through analytics grouped by venue event.</summary>
    [HttpGet("my/analytics")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<EventVenueAnalyticsDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAnalytics(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(
            new GetVendorEventPackageAnalyticsQuery(userId.Value),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/analytics")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<EventPackageAnalyticsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalytics(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(
            new GetEventPackageAnalyticsQuery(id, userId.Value, IsAdmin()),
            cancellationToken);

        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : NotFound(result);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<EventParkingPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(new GetEventPackageByIdQuery(id), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Vendor: packages for own listings.</summary>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<EventParkingPackageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetVendorEventPackagesQuery(userId.Value), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<EventParkingPackageDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateEventParkingPackageDto dto, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new CreateEventParkingPackageCommand(userId.Value, IsAdmin(), dto),
            cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<EventParkingPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEventParkingPackageDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new UpdateEventParkingPackageCommand(id, userId.Value, IsAdmin(), dto),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new DeactivateEventParkingPackageCommand(id, userId.Value, IsAdmin()),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Guest: purchase prepaid event package → booking.</summary>
    [HttpPost("{id:guid}/purchase")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Purchase(
        Guid id,
        [FromBody] PurchaseEventParkingPackageDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new PurchaseEventParkingPackageCommand(
                id,
                userId.Value,
                dto.VehicleType,
                dto.VehicleNumber,
                dto.VehicleModel,
                dto.VehicleColor),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool IsAdmin() =>
        User.IsInRole("Admin") || User.IsInRole("admin");
}
