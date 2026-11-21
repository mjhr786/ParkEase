using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Application.Commands.Payments;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Queries.Payments;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.UnitTests.API;

public class PaymentsControllerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly Mock<IUserLookup> _userLookup = new();
    private readonly Mock<IHostEnvironment> _environment = new();
    private readonly PaymentsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public PaymentsControllerTests()
    {
        _environment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        _controller = new PaymentsController(
            _dispatcher.Object,
            _configuration.Object,
            _userLookup.Object,
            Options.Create(new UpiPaymentsOptions { Enabled = false }),
            _environment.Object);
        SetUser(_userId);
    }

    private void SetUser(Guid? userId)
    {
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"))
            }
        };
    }

    [Fact]
    public void GetStripeConfig_ReturnsPublishableKey()
    {
        _configuration.Setup(c => c["Stripe:PublishableKey"]).Returns("pk_test_abc");

        var result = _controller.GetStripeConfig();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var prop = ok.Value!.GetType().GetProperty("publishableKey");
        prop!.GetValue(ok.Value).Should().Be("pk_test_abc");
    }

    [Fact]
    public async Task GetConfig_Anonymous_OmitsUpiContactRequired()
    {
        SetUser(null);
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        _configuration.Setup(c => c["Stripe:PublishableKey"]).Returns("pk_live");

        var result = await _controller.GetConfig(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<PaymentsConfigDto>>().Subject;
        body.Data.Should().NotBeNull();
        body.Data!.UpiEnabled.Should().BeFalse();
        body.Data.StripePublishableKey.Should().Be("pk_live");
        body.Data.UpiContactRequired.Should().BeNull();
    }

    [Fact]
    public async Task GetConfig_JwtEmptyPhone_UpiContactRequiredTrue()
    {
        _userLookup
            .Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "a@b.com", "A", "B", PhoneNumber: null));
        _configuration.Setup(c => c["Stripe:PublishableKey"]).Returns("pk_test");

        var result = await _controller.GetConfig(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<PaymentsConfigDto>>().Subject;
        body.Data!.UpiContactRequired.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_WhenNoUser_ReturnsUnauthorized()
    {
        SetUser(null);

        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsOk()
    {
        var response = new ApiResponse<PaymentDto>(true, null, null);
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetPaymentByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetByBookingId_WhenMissing_ReturnsNotFound()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetPaymentByBookingIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<PaymentDto>(false, "not found", null));

        var result = await _controller.GetByBookingId(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateOrder_WithRawGuidJson_ReturnsOk()
    {
        var bookingId = Guid.NewGuid();
        var body = JsonDocument.Parse($"\"{bookingId}\"").RootElement;
        var response = new ApiResponse<string>(true, null, "pi_secret");
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreatePaymentOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.CreateOrder(body, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<CreatePaymentOrderCommand>(c => c.UserId == _userId && c.BookingId == bookingId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithObjectBodyAndOverstay_ReturnsOk()
    {
        var bookingId = Guid.NewGuid();
        var body = JsonDocument.Parse($$"""{"bookingId":"{{bookingId}}","payOverstayFee":true}""").RootElement;
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreatePaymentOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<string>(true, null, "secret"));

        var result = await _controller.CreateOrder(body, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<CreatePaymentOrderCommand>(c => c.BookingId == bookingId && c.PayOverstayFee == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidBody_ReturnsBadRequest()
    {
        var body = JsonDocument.Parse("{}").RootElement;

        var result = await _controller.CreateOrder(body, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(It.IsAny<CreatePaymentOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPayment_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<ProcessPaymentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<PaymentResultDto>(false, "declined", null));

        var result = await _controller.ProcessPayment(
            new CreatePaymentDto(Guid.NewGuid(), PaymentMethod.CreditCard),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task VerifyPayment_WhenSuccess_ReturnsOk()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<VerifyPaymentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<PaymentResultDto>(true, null, new PaymentResultDto(true, "tx", PaymentStatus.Completed, null, null)));

        var result = await _controller.VerifyPayment(
            new VerifyPaymentDto
            {
                BookingId = Guid.NewGuid(),
                RazorpayPaymentId = "pay_123",
                RazorpayOrderId = "order_1",
                RazorpaySignature = "sig"
            },
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ProcessRefund_WhenSuccess_ReturnsOk()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<ProcessRefundCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<RefundResultDto>(true, null, new RefundResultDto(true, "rf_1", 10m, null)));

        var result = await _controller.ProcessRefund(
            new RefundRequestDto(Guid.NewGuid(), 10m, "customer request"),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
