using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Enums;

namespace ParkingApp.API.Controllers;

/// <summary>
/// Bookings controller using CQRS pattern
/// </summary>
[ApiController]
[Route("api/v2/[controller]")]
[Authorize]
[Produces("application/json")]
public class BookingsV2Controller : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public BookingsV2Controller(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Get booking by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = new GetBookingByIdQuery(id, userId.Value);
        var result = await _dispatcher.QueryAsync(query, cancellationToken);

        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Get booking by reference number
    /// </summary>
    [HttpGet("reference/{reference}")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByReference(string reference, CancellationToken cancellationToken)
    {
        var query = new GetBookingByReferenceQuery(reference);
        var result = await _dispatcher.QueryAsync(query, cancellationToken);

        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>
    /// Get current user's bookings
    /// </summary>
    [HttpGet("my-bookings")]
    [ProducesResponseType(typeof(ApiResponse<BookingListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyBookings([FromQuery] BookingFilterDto? filter, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = new GetUserBookingsQuery(userId.Value, filter);
        var result = await _dispatcher.QueryAsync(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get bookings for vendor's parking spaces
    /// </summary>
    [HttpGet("vendor")]
    [Authorize(Roles = "Vendor,Admin")]
    [ProducesResponseType(typeof(ApiResponse<BookingListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVendorBookings([FromQuery] BookingFilterDto? filter, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = new GetVendorBookingsQuery(userId.Value, filter);
        var result = await _dispatcher.QueryAsync(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Calculate price for a booking
    /// </summary>
    [HttpPost("calculate-price")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PriceBreakdownDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalculatePrice([FromBody] PriceCalculationDto dto, CancellationToken cancellationToken)
    {
        var query = new CalculatePriceQuery(
            dto.ParkingSpaceId,
            dto.StartDateTime,
            dto.EndDateTime,
            (int)dto.PricingType,
            dto.DiscountCode
        );
        var result = await _dispatcher.QueryAsync(query, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Create a new booking
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new CreateBookingCommand(
            userId.Value,
            dto.ParkingSpaceId,
            dto.StartDateTime,
            dto.EndDateTime,
            dto.PricingType,
            dto.VehicleType,
            dto.VehicleNumber,
            dto.VehicleModel,
            dto.DiscountCode
        );

        var result = await _dispatcher.SendAsync(command, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
    }

    /// <summary>
    /// Cancel a booking
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelBookingDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new CancelBookingCommand(id, userId.Value, dto.Reason ?? "Cancelled by user");
        var result = await _dispatcher.SendAsync(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Approve a booking (vendor only)
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Vendor,Admin")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new ApproveBookingCommand(id, userId.Value);
        var result = await _dispatcher.SendAsync(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Reject a booking (vendor only)
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Vendor,Admin")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBookingDto? dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new RejectBookingCommand(id, userId.Value, dto?.Reason);
        var result = await _dispatcher.SendAsync(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Check in to a booking
    /// </summary>
    [HttpPost("{id:guid}/check-in")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckIn(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new CheckInCommand(id, userId.Value);
        var result = await _dispatcher.SendAsync(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Check out from a booking
    /// </summary>
    [HttpPost("{id:guid}/check-out")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckOut(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new CheckOutCommand(id, userId.Value);
        var result = await _dispatcher.SendAsync(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

/// <summary>
/// DTO for rejecting a booking
/// </summary>
public record RejectBookingDto(string? Reason);
