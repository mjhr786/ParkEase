using FluentAssertions;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Services;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class PaymentObservabilityTests
{
    [Fact]
    public void RedactIntentUrl_StripsPaAndKeepsAmount()
    {
        PaymentObservability.RedactIntentUrl("upi://pay?pa=merchant@razorpay&pn=ParkEase&am=150.00&cu=INR")
            .Should().Be("upi://pay?pa=***&am=150.00");
    }

    [Fact]
    public void RedactIntentUrl_NeverReturnsRawVpa()
    {
        var redacted = PaymentObservability.RedactIntentUrl("upi://pay?pa=secret-vpa@okhdfcbank&am=10");
        redacted.Should().NotContain("secret-vpa");
        redacted.Should().StartWith("upi://pay?pa=***");
    }

    [Fact]
    public void CaptureRefundReason_MapsKdPay41()
    {
        PaymentObservability.CaptureRefundReason(PaymentCaptureOutcome.CannotAccept)
            .Should().Be(PaymentObservability.ReasonCannotConfirm);
        PaymentObservability.CaptureRefundReason(PaymentCaptureOutcome.AlreadyCapturedDifferentId)
            .Should().Be(PaymentObservability.ReasonOtherCharge);
    }

    [Fact]
    public void EventNames_MatchDesignObservabilityTable()
    {
        PaymentObservability.MatcherMismatch.Should().Be("PaymentCaptureMatcherMismatch");
        PaymentObservability.NeedsRefund.Should().Be("PaymentNeedsRefund");
        PaymentObservability.ExpireJobCompleted.Should().Be("ExpireJobCompleted");
        PaymentObservability.RefundPermanentFailure.Should().Be("PaymentRefundPermanentFailure");
    }
}
