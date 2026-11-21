using System.Collections.Concurrent;
using ParkingApp.Marketplace.Application.Interfaces;

namespace ParkingApp.IntegrationTests.Support;

/// <summary>Fake Razorpay UPI adapter: returns an unmodified <c>upi://pay?...</c> intent URI.</summary>
public sealed class FakeUpiCheckoutService : IUpiCheckoutService
{
    public UpiQrSessionRequest? LastRequest { get; private set; }
    public int CallCount { get; private set; }
    public string DefaultStatus { get; set; } = "captured";
    public bool RefundShouldFail { get; set; }
    public TimeSpan? GetPaymentDelay { get; set; }
    public ConcurrentDictionary<string, UpiPaymentSnapshot> Payments { get; } = new(StringComparer.Ordinal);
    public ConcurrentBag<RefundCall> Refunds { get; } = new();

    public Task<UpiQrSession> CreateQrSessionAsync(
        UpiQrSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        CallCount++;
        var orderId = $"order_test_{Guid.NewGuid():N}"[..20];
        var payId = $"pay_test_{Guid.NewGuid():N}"[..18];
        var intent =
            $"upi://pay?pa=parkease@razorpay&pn=ParkEase&am={request.Amount}&cu=INR&tn={Uri.EscapeDataString(orderId)}";
        return Task.FromResult(new UpiQrSession(orderId, payId, intent, "parkease@razorpay"));
    }

    public async Task<UpiPaymentSnapshot> GetPaymentAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default)
    {
        if (GetPaymentDelay is TimeSpan delay)
            await Task.Delay(delay, cancellationToken);

        if (Payments.TryGetValue(providerPaymentId, out var snapshot))
            return snapshot;

        foreach (var item in Payments.Values)
        {
            if (string.Equals(item.OrderId, providerPaymentId, StringComparison.Ordinal))
                return item;
        }

        return new UpiPaymentSnapshot(
            providerPaymentId,
            DefaultStatus,
            0,
            "INR",
            null,
            new Dictionary<string, string>());
    }

    public Task<UpiRefundResult> RefundAsync(
        string providerPaymentId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
    {
        Refunds.Add(new RefundCall(providerPaymentId, amount, currency));
        if (RefundShouldFail)
            return Task.FromResult(new UpiRefundResult(false, null, "failed", "refund failed"));
        return Task.FromResult(new UpiRefundResult(true, $"rfnd_{providerPaymentId}", "processed", null));
    }

    public bool MatchesExpected(UpiPaymentSnapshot snapshot, PaymentCaptureCheck expected)
    {
        if (expected.BookingId == Guid.Empty || expected.Amount <= 0)
            return false;
        if (snapshot.AmountMinor > 0 && snapshot.AmountMinor != (long)decimal.Round(expected.Amount * 100m, 0, MidpointRounding.AwayFromZero))
            return false;
        if (!string.IsNullOrWhiteSpace(snapshot.Currency)
            && !string.Equals(snapshot.Currency, expected.Currency, StringComparison.OrdinalIgnoreCase))
            return false;
        if (snapshot.Notes.TryGetValue("bookingId", out var bookingIdValue)
            && Guid.TryParse(bookingIdValue, out var bookingId)
            && bookingId != expected.BookingId)
            return false;
        if (snapshot.Notes.TryGetValue("purpose", out var purpose)
            && !string.Equals(purpose, expected.Purpose, StringComparison.Ordinal))
            return false;
        return true;
    }

    public void ResetWebhookState()
    {
        Payments.Clear();
        while (Refunds.TryTake(out _)) { }
        RefundShouldFail = false;
        GetPaymentDelay = null;
        DefaultStatus = "captured";
    }

    public sealed record RefundCall(string ProviderPaymentId, decimal Amount, string Currency);
}
