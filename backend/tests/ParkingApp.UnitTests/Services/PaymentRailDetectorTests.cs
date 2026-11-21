using FluentAssertions;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class PaymentRailDetectorTests
{
    [Fact]
    public void Detect_OrderRailWinsOverHint()
    {
        var order = PaymentOrder.Create(
            Guid.NewGuid(), "booking", PaymentOrder.RailUpi, PaymentOrder.ProviderRazorpay,
            "order_abc", 10m, "INR", PaymentOrderStatus.Pending, "pay_abc");

        var decision = PaymentRailDetector.Detect(order, "pay_abc", "order_abc", "stripe", "stripe");

        decision.Mismatch.Should().BeTrue();
        decision.Rail.Should().Be(PaymentRailDetector.Upi);
    }

    [Fact]
    public void Detect_PiPrefixIsStripe()
    {
        var decision = PaymentRailDetector.Detect(null, "pi_abc", "pi_abc", "stripe", null);

        decision.Mismatch.Should().BeFalse();
        decision.Rail.Should().Be(PaymentRailDetector.Stripe);
    }

    [Fact]
    public void Detect_PayPrefixIsUpi_SignatureOptional()
    {
        var decision = PaymentRailDetector.Detect(null, "pay_abc", "order_abc", null, null);

        decision.Mismatch.Should().BeFalse();
        decision.Rail.Should().Be(PaymentRailDetector.Upi);
    }

    [Fact]
    public void Detect_HintContradictingPrefix_IsMismatch()
    {
        var decision = PaymentRailDetector.Detect(null, "pi_abc", "pi_abc", null, "upi");

        decision.Mismatch.Should().BeTrue();
    }

    [Fact]
    public void Detect_PaymentPiWinsOverOrderPrefix()
    {
        var decision = PaymentRailDetector.Detect(null, "pi_original", "order_original", "s", null);

        decision.Mismatch.Should().BeFalse();
        decision.Rail.Should().Be(PaymentRailDetector.Stripe);
    }
}
