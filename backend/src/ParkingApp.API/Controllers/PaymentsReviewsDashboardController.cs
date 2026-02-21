using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Payments;
using ParkingApp.Application.CQRS.Commands.Reviews;
using ParkingApp.Application.CQRS.Queries.Dashboard;
using ParkingApp.Application.CQRS.Queries.Payments;
using ParkingApp.Application.CQRS.Queries.Reviews;
using ParkingApp.Application.DTOs;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IConfiguration _configuration;

    public PaymentsController(IDispatcher dispatcher, IConfiguration configuration)
    {
        _dispatcher = dispatcher;
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
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetPaymentByIdQuery(id, userId.Value), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("booking/{bookingId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByBookingId(Guid bookingId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetPaymentByBookingIdQuery(bookingId, userId.Value), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessPayment([FromBody] CreatePaymentDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new ProcessPaymentCommand(userId.Value, dto), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("create-order")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOrder([FromBody] Guid bookingId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new CreatePaymentOrderCommand(userId.Value, bookingId), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("verify")]
    [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new VerifyPaymentCommand(userId.Value, dto), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("refund")]
    [ProducesResponseType(typeof(ApiResponse<RefundResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessRefund([FromBody] RefundRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new ProcessRefundCommand(userId.Value, dto), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IValidator<CreateReviewDto> _createValidator;

    public ReviewsController(IDispatcher dispatcher, IValidator<CreateReviewDto> createValidator)
    {
        _dispatcher = dispatcher;
        _createValidator = createValidator;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.QueryAsync(new GetReviewByIdQuery(id), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("parking-space/{parkingSpaceId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<List<ReviewDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByParkingSpace(Guid parkingSpaceId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.QueryAsync(new GetReviewsByParkingSpaceQuery(parkingSpaceId), cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateReviewDto dto, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<ReviewDto>(false, "Validation failed", null,
                validation.Errors.Select(e => e.ErrorMessage).ToList()));

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new CreateReviewCommand(userId.Value, dto), cancellationToken);
        return result.Success ? Created($"/api/reviews/{result.Data?.Id}", result) : BadRequest(result);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReviewDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new UpdateReviewCommand(id, userId.Value, dto), cancellationToken);
        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        return Ok(result);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new DeleteReviewCommand(id, userId.Value), cancellationToken);
        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        return Ok(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpPost("{id:guid}/owner-response")]
    [ProducesResponseType(typeof(ApiResponse<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddOwnerResponse(Guid id, [FromBody] OwnerResponseDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new AddOwnerResponseCommand(id, userId.Value, dto), cancellationToken);
        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
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
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public DashboardController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("vendor")]
    [Authorize(Roles = "Vendor,Admin")]
    [ProducesResponseType(typeof(ApiResponse<VendorDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVendorDashboard(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetVendorDashboardQuery(userId.Value), cancellationToken);
        return Ok(result);
    }

    [HttpGet("member")]
    [ProducesResponseType(typeof(ApiResponse<MemberDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMemberDashboard(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetMemberDashboardQuery(userId.Value), cancellationToken);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

