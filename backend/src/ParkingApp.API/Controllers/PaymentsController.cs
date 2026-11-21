using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Commands.Payments;
using ParkingApp.Marketplace.Application.Commands.Reviews;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Queries.Dashboard;
using ParkingApp.Marketplace.Application.Queries.Payments;
using ParkingApp.Marketplace.Application.Queries.Reviews;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;


namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IConfiguration _configuration;
    private readonly IUserLookup _userLookup;
    private readonly IOptions<UpiPaymentsOptions> _upiOptions;
    private readonly IHostEnvironment _environment;

    public PaymentsController(
        IDispatcher dispatcher,
        IConfiguration configuration,
        IUserLookup userLookup,
        IOptions<UpiPaymentsOptions> upiOptions,
        IHostEnvironment environment)
    {
        _dispatcher = dispatcher;
        _configuration = configuration;
        _userLookup = userLookup;
        _upiOptions = upiOptions;
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpGet("stripe-config")]
    public IActionResult GetStripeConfig()
    {
        var publishableKey = _configuration["Stripe:PublishableKey"];
        return Ok(new { publishableKey });
    }

    [AllowAnonymous]
    [HttpGet("config")]
    [ProducesResponseType(typeof(ApiResponse<PaymentsConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
    {
        bool? upiContactRequired = null;
        var userId = GetUserId();
        if (userId is not null)
        {
            var user = await _userLookup.GetByIdAsync(userId.Value, cancellationToken);
            upiContactRequired = string.IsNullOrWhiteSpace(user?.PhoneNumber);
        }

        var dto = new PaymentsConfigDto(
            _upiOptions.Value.Enabled,
            _configuration["Stripe:PublishableKey"],
            upiContactRequired);
        return Ok(new ApiResponse<PaymentsConfigDto>(true, null, dto));
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
    public async Task<IActionResult> CreateOrder([FromBody] System.Text.Json.JsonElement body, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // Supports legacy raw GUID JSON string and { bookingId, payOverstayFee }.
        if (!TryParseCreateOrderBody(body, out var bookingId, out var payOverstayFee))
            return BadRequest(new ApiResponse<string>(false, "bookingId is required", null));

        var result = await _dispatcher.SendAsync(
            new CreatePaymentOrderCommand(userId.Value, bookingId, payOverstayFee),
            cancellationToken);
        return MapPaymentResult(result);
    }

    [HttpPost("upi/create-order")]
    [ProducesResponseType(typeof(ApiResponse<UpiCheckoutDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UpiCheckoutDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<UpiCheckoutDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUpiOrder([FromBody] UpiCreateOrderDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new CreateUpiPaymentOrderCommand(
                userId.Value,
                dto.BookingId,
                dto.PayOverstayFee,
                ResolveForwardedClientIp(),
                Request.Headers.Referer.ToString(),
                ResolveUserAgent(),
                _environment.IsDevelopment()),
            cancellationToken);
        return MapPaymentResult(result);
    }

    [HttpGet("upi/orders/{orderId}")]
    [ProducesResponseType(typeof(ApiResponse<UpiCheckoutDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UpiCheckoutDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUpiOrder(string orderId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetUpiPaymentOrderQuery(userId.Value, orderId), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    private static bool TryParseCreateOrderBody(
        System.Text.Json.JsonElement body,
        out Guid bookingId,
        out bool? payOverstayFee)
    {
        bookingId = Guid.Empty;
        payOverstayFee = null;

        if (body.ValueKind == System.Text.Json.JsonValueKind.String)
            return Guid.TryParse(body.GetString(), out bookingId);

        if (body.ValueKind != System.Text.Json.JsonValueKind.Object)
            return false;

        if (!body.TryGetProperty("bookingId", out var idEl) && !body.TryGetProperty("BookingId", out idEl))
            return false;

        if (idEl.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            if (!Guid.TryParse(idEl.GetString(), out bookingId))
                return false;
        }
        else if (!idEl.TryGetGuid(out bookingId))
        {
            return false;
        }

        if (body.TryGetProperty("payOverstayFee", out var feeEl) || body.TryGetProperty("PayOverstayFee", out feeEl))
        {
            if (feeEl.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
                payOverstayFee = feeEl.GetBoolean();
        }

        return bookingId != Guid.Empty;
    }

    [HttpPost("verify")]
    [ProducesResponseType(typeof(ApiResponse<PaymentResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new VerifyPaymentCommand(userId.Value, dto), cancellationToken);
        return MapPaymentResult(result);
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

    private IActionResult MapPaymentResult<T>(ApiResponse<T> result)
    {
        if (result.Success)
            return Ok(result);
        if (string.Equals(result.Code, PaymentErrorCodes.PaymentInProgress, StringComparison.Ordinal))
            return Conflict(result);
        return BadRequest(result);
    }

    private string? ResolveForwardedClientIp()
    {
        var ip = HttpContext.Connection.RemoteIpAddress;
        if (ip is null)
            return null;
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        return ip.ToString();
    }

    private string ResolveUserAgent()
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        if (!string.IsNullOrWhiteSpace(userAgent))
            return userAgent;
        var client = Request.Headers["X-ParkEase-Client"].ToString();
        if (string.Equals(client, "mobile", StringComparison.OrdinalIgnoreCase))
            return "ParkEase/mobile";
        return "ParkEase/web";
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

    [Authorize(Roles = "User,Admin")]
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
    [Authorize(Roles = "User,Admin")]
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

