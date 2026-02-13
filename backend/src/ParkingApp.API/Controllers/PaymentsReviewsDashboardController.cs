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
public class PaymentsController : ControllerBase
{
    private readonly IPaymentAppService _paymentService;
    private readonly IConfiguration _configuration;

    public PaymentsController(IPaymentAppService paymentService, IConfiguration configuration)
    {
        _paymentService = paymentService;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpGet("stripe-config")]
    public IActionResult GetStripeConfig()
    {
        var publishableKey = _configuration["Stripe:PublishableKey"];
        return Ok(new { publishableKey });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _paymentService.GetByIdAsync(id, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpGet("booking/{bookingId:guid}")]
    public async Task<IActionResult> GetByBookingId(Guid bookingId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _paymentService.GetByBookingIdAsync(bookingId, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> ProcessPayment([FromBody] CreatePaymentDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _paymentService.ProcessPaymentAsync(userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("create-order")]
    public async Task<IActionResult> CreateOrder([FromBody] Guid bookingId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _paymentService.CreateRazorpayOrderAsync(userId.Value, bookingId, cancellationToken);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _paymentService.ProcessRazorpayPaymentAsync(userId.Value, dto, cancellationToken);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("refund")]
    public async Task<IActionResult> ProcessRefund([FromBody] RefundRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _paymentService.ProcessRefundAsync(userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly IValidator<CreateReviewDto> _createValidator;

    public ReviewsController(IReviewService reviewService, IValidator<CreateReviewDto> createValidator)
    {
        _reviewService = reviewService;
        _createValidator = createValidator;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetByIdAsync(id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpGet("parking-space/{parkingSpaceId:guid}")]
    public async Task<IActionResult> GetByParkingSpace(Guid parkingSpaceId, CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetByParkingSpaceAsync(parkingSpaceId, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewDto dto, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<ReviewDto>(false, "Validation failed", null,
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _reviewService.CreateAsync(userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Created($"/api/reviews/{result.Data?.Id}", result);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReviewDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _reviewService.UpdateAsync(id, userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _reviewService.DeleteAsync(id, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpPost("{id:guid}/owner-response")]
    public async Task<IActionResult> AddOwnerResponse(Guid id, [FromBody] OwnerResponseDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _reviewService.AddOwnerResponseAsync(id, userId.Value, dto, cancellationToken);
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

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("vendor")]
    [Authorize(Roles = "Vendor,Admin")]
    public async Task<IActionResult> GetVendorDashboard(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _dashboardService.GetVendorDashboardAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpGet("member")]
    public async Task<IActionResult> GetMemberDashboard(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _dashboardService.GetMemberDashboardAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
