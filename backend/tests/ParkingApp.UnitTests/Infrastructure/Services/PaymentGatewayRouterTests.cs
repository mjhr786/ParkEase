using FluentAssertions;
using Moq;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Infrastructure.Services;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class PaymentGatewayRouterTests
{
    private readonly Mock<IPaymentService> _stripe = new();
    private readonly Mock<IUpiCheckoutService> _upi = new();
    private readonly PaymentGatewayRouter _router;

    public PaymentGatewayRouterTests()
    {
        _router = new PaymentGatewayRouter(_stripe.Object, _upi.Object);
    }

    [Fact]
    public async Task ProcessRefundAsync_GatewayTransactionIdStartsWithPi_DelegatesToStripe()
    {
        var request = new RefundRequest
        {
            PaymentId = Guid.NewGuid(),
            Amount = 10m,
            Reason = "test",
            GatewayTransactionId = "pi_abc"
        };
        var stripeResult = new RefundResult
        {
            Success = true,
            RefundTransactionId = "re_stripe",
            RefundedAmount = 10m
        };
        _stripe
            .Setup(s => s.ProcessRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stripeResult);

        var result = await _router.ProcessRefundAsync(request);

        result.Should().BeSameAs(stripeResult);
        _stripe.Verify(
            s => s.ProcessRefundAsync(
                It.Is<RefundRequest>(r => r.GatewayTransactionId == "pi_abc"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessRefundAsync_GatewayTransactionIdStartsWithPay_DelegatesToRazorpay()
    {
        var request = new RefundRequest
        {
            PaymentId = Guid.NewGuid(),
            Amount = 10m,
            Reason = "test",
            GatewayTransactionId = "pay_abc"
        };
        _upi.Setup(u => u.RefundAsync("pay_abc", 10m, "INR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpiRefundResult(true, "rfnd_1", "processed", null));

        var result = await _router.ProcessRefundAsync(request);

        result.Success.Should().BeTrue();
        result.RefundTransactionId.Should().Be("rfnd_1");
        result.RefundedAmount.Should().Be(10m);
        _stripe.Verify(
            s => s.ProcessRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _upi.Verify(u => u.RefundAsync("pay_abc", 10m, "INR", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefundAsync_PendingRazorpayStatus_IsSuccess()
    {
        _upi.Setup(u => u.RefundAsync("pay_abc", 10m, "INR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpiRefundResult(true, "rfnd_p", "pending", null));

        var result = await _router.ProcessRefundAsync(new RefundRequest
        {
            Amount = 10m,
            GatewayTransactionId = "pay_abc"
        });

        result.Success.Should().BeTrue();
        result.RefundTransactionId.Should().Be("rfnd_p");
    }

    [Fact]
    public async Task ProcessRefundAsync_MockGateway_FailsClosed()
    {
        var result = await _router.ProcessRefundAsync(new RefundRequest
        {
            Amount = 10m,
            GatewayTransactionId = "pi_abc",
            PaymentGateway = "MockGateway"
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("MockGateway refunds are not available");
        _stripe.Verify(
            s => s.ProcessRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessRefundAsync_MissingGatewayTransactionId_FailsClosedWithoutCallingStripe(string? gatewayTransactionId)
    {
        var request = new RefundRequest
        {
            PaymentId = Guid.NewGuid(),
            Amount = 10m,
            Reason = "test",
            GatewayTransactionId = gatewayTransactionId
        };

        var result = await _router.ProcessRefundAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Missing gateway transaction id for refund");
        _stripe.Verify(
            s => s.ProcessRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessRefundAsync_UnknownGatewayTransactionId_FailsClosedWithoutCallingStripe()
    {
        var request = new RefundRequest
        {
            PaymentId = Guid.NewGuid(),
            Amount = 10m,
            Reason = "test",
            GatewayTransactionId = "ch_abc"
        };

        var result = await _router.ProcessRefundAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Unknown payment gateway; refunds are not available");
        _stripe.Verify(
            s => s.ProcessRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_DelegatesToStripe()
    {
        _stripe
            .Setup(s => s.CreateOrderAsync(50m, "INR", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("pi_client_secret");

        var result = await _router.CreateOrderAsync(50m);

        result.Should().Be("pi_client_secret");
        _stripe.Verify(
            s => s.CreateOrderAsync(50m, "INR", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_DelegatesToStripe()
    {
        var request = new PaymentRequest { Amount = 25m, Currency = "INR" };
        var stripeResult = new PaymentResult { Success = true, Status = PaymentStatus.Pending };
        _stripe
            .Setup(s => s.ProcessPaymentAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stripeResult);

        var result = await _router.ProcessPaymentAsync(request);

        result.Should().BeSameAs(stripeResult);
    }

    [Fact]
    public async Task VerifyPaymentSignatureAsync_DelegatesToStripe()
    {
        _stripe
            .Setup(s => s.VerifyPaymentSignatureAsync("pi_1", "order_1", "sig", It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(true);

        var result = await _router.VerifyPaymentSignatureAsync("pi_1", "order_1", "sig");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetPaymentStatusAsync_DelegatesToStripe()
    {
        _stripe
            .Setup(s => s.GetPaymentStatusAsync("pi_abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentStatus.Completed);

        var result = await _router.GetPaymentStatusAsync("pi_abc");

        result.Should().Be(PaymentStatus.Completed);
    }
}
