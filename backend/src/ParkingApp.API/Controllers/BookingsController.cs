using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly IValidator<CreateBookingDto> _createValidator;

    public BookingsController(
        IBookingService bookingService,
        IValidator<CreateBookingDto> createValidator)
    {
        _bookingService = bookingService;
        _createValidator = createValidator;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.GetByIdAsync(id, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpGet("reference/{reference}")]
    public async Task<IActionResult> GetByReference(string reference, CancellationToken cancellationToken)
    {
        var result = await _bookingService.GetByReferenceAsync(reference, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpGet("my-bookings")]
    public async Task<IActionResult> GetMyBookings([FromQuery] BookingFilterDto? filter, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.GetByUserAsync(userId.Value, filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("parking-space/{parkingSpaceId:guid}")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetByParkingSpace(Guid parkingSpaceId, [FromQuery] BookingFilterDto? filter, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.GetByParkingSpaceAsync(parkingSpaceId, userId.Value, filter, cancellationToken);
        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("calculate-price")]
    [AllowAnonymous]
    public async Task<IActionResult> CalculatePrice([FromBody] PriceCalculationDto dto, CancellationToken cancellationToken)
    {
        var result = await _bookingService.CalculatePriceAsync(dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<BookingDto>(false, "Validation failed", null,
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.CreateAsync(userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Created($"/api/bookings/{result.Data?.Id}", result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBookingDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.UpdateAsync(id, userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelBookingDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.CancelAsync(id, userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/check-in")]
    public async Task<IActionResult> CheckIn(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.CheckInAsync(id, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/check-out")]
    public async Task<IActionResult> CheckOut(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.CheckOutAsync(id, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("vendor-bookings")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetVendorBookings([FromQuery] BookingFilterDto? filter, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.GetVendorBookingsAsync(userId.Value, filter, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.ApproveAsync(id, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBookingDto? dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _bookingService.RejectAsync(id, userId.Value, dto?.Reason, cancellationToken);
        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
