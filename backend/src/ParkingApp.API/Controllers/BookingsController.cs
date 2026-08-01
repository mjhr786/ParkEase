using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Commands.Bookings;
using ParkingApp.Marketplace.Application.Queries.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;

using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.API.Controllers;

/// <summary>
/// Bookings controller using CQRS pattern
/// </summary>
[ApiController]
[Route("api/bookings")]
[Authorize]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public BookingsController(IDispatcher dispatcher)
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
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = new GetBookingByReferenceQuery(reference, userId.Value);
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
    [HttpGet("vendor-bookings")]
    [Authorize(Roles = "User,Admin")]
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
    /// Get the count of pending booking requests for the vendor
    /// </summary>
    [HttpGet("pending-count")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingRequestsCount(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = new GetPendingRequestsCountQuery(userId.Value);
        var result = await _dispatcher.QueryAsync(query, cancellationToken);
        
        return Ok(result);
    }

    /// <summary>
    /// Get bookings for a specific parking space (vendor only)
    /// </summary>
    [HttpGet("parking-space/{parkingSpaceId:guid}")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(ApiResponse<BookingListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByParkingSpace(Guid parkingSpaceId, [FromQuery] BookingFilterDto? filter, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = new GetBookingsByParkingSpaceQuery(parkingSpaceId, userId.Value, filter);
        var result = await _dispatcher.QueryAsync(query, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Digital access pass (QR token + image URL + wallet flags) for a booking.
    /// </summary>
    [HttpGet("{id:guid}/access-pass")]
    [ProducesResponseType(typeof(ApiResponse<BookingAccessPassDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingAccessPassDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAccessPass(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetBookingAccessPassQuery(id, userId.Value), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Download Apple Wallet .pkpass for a booking access pass.
    /// </summary>
    [HttpGet("{id:guid}/access-pass/apple.pkpass")]
    [Produces("application/vnd.apple.pkpass", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AppleWalletPassFileDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAppleWalletPass(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetAppleWalletPassQuery(id, userId.Value), cancellationToken);
        if (!result.Success || result.Data is null)
            return BadRequest(result);

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    /// <summary>
    /// Google Wallet save-to-wallet URL (JWT) for a booking access pass.
    /// </summary>
    [HttpGet("{id:guid}/access-pass/google-wallet")]
    [ProducesResponseType(typeof(ApiResponse<GoogleWalletSaveLinkDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GoogleWalletSaveLinkDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetGoogleWalletSaveLink(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetGoogleWalletSaveLinkQuery(id, userId.Value), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Verify a scanned digital access-pass token (guest or facility owner).
    /// </summary>
    [HttpPost("access-pass/verify")]
    [ProducesResponseType(typeof(ApiResponse<AccessPassVerifyResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyAccessPass([FromBody] VerifyAccessPassDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _dispatcher.QueryAsync(
            new VerifyAccessPassQuery(dto.Token, userId),
            cancellationToken);

        if (!result.Success && string.Equals(result.Message, "Unauthorized", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status403Forbidden, result);

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
        var userId = GetUserId();
        var query = new CalculatePriceQuery(
            dto.ParkingSpaceId,
            dto.StartDateTime,
            dto.EndDateTime,
            (int)dto.PricingType,
            dto.DiscountCode,
            userId,
            dto.IncludeEvCharging,
            dto.AncillaryServiceIds
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
            dto.SlotNumber,
            dto.VehicleNumber,
            dto.VehicleModel,
            dto.VehicleColor,
            dto.DiscountCode,
            dto.IncludeEvCharging,
            dto.AncillaryServiceIds
        );

        var result = await _dispatcher.SendAsync(command, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
    }

    /// <summary>
    /// Update an existing booking
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBookingDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new UpdateBookingCommand(id, userId.Value, dto);
        var result = await _dispatcher.SendAsync(command, cancellationToken);

        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
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
    [Authorize(Roles = "User,Admin")]
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
    [Authorize(Roles = "User,Admin")]
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

    /// <summary>Guest: request valet vehicle retrieval (~10 min lead by default).</summary>
    [HttpPost("{id:guid}/valet/request")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestValet(Guid id, [FromBody] RequestValetDto? dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new RequestValetCommand(id, userId.Value, dto?.Notes, dto?.LeadMinutes),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Guest: cancel an open valet request.</summary>
    [HttpPost("{id:guid}/valet/cancel")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelValet(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new CancelValetCommand(id, userId.Value), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Vendor: acknowledge valet request (retrieving vehicle).</summary>
    [HttpPost("{id:guid}/valet/acknowledge")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcknowledgeValet(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new AcknowledgeValetCommand(id, userId.Value), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Vendor: mark vehicle ready for guest pickup.</summary>
    [HttpPost("{id:guid}/valet/ready")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkValetReady(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new MarkValetReadyCommand(id, userId.Value), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Vendor: complete valet handoff.</summary>
    [HttpPost("{id:guid}/valet/complete")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteValet(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new CompleteValetCommand(id, userId.Value), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Vendor: assign or override indoor bay / level / zone guidance.</summary>
    [HttpPost("{id:guid}/bay-assignment")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignBay(Guid id, [FromBody] AssignBayDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new AssignBayCommand(id, userId.Value, dto.FacilityLevel, dto.FacilityZone, dto.BayLabel, dto.SlotNumber),
            cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Request an extension for a booking (creates pending extension request for vendor approval)
    /// </summary>
    [HttpPost("{id:guid}/extend")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestExtension(Guid id, [FromBody] ExtendBookingDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new RequestExtensionCommand(id, userId.Value, dto.NewEndDateTime, dto.PricingType);
        var result = await _dispatcher.SendAsync(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Approve a pending extension request (vendor only)
    /// </summary>
    [HttpPost("{id:guid}/approve-extension")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveExtension(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new ApproveExtensionCommand(id, userId.Value);
        var result = await _dispatcher.SendAsync(command, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Reject a pending extension request (vendor only)
    /// </summary>
    [HttpPost("{id:guid}/reject-extension")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectExtension(Guid id, [FromBody] RejectBookingDto? dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new RejectExtensionCommand(id, userId.Value, dto?.Reason);
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

