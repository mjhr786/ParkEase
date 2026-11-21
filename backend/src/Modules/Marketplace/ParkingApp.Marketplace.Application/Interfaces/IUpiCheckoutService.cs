namespace ParkingApp.Marketplace.Application.Interfaces;

public sealed record UpiQrSessionRequest(
    decimal Amount,
    string Currency,
    string Receipt,
    string Email,
    string Contact,
    string ClientIp,
    string Referer,
    string UserAgent,
    IReadOnlyDictionary<string, string>? Notes = null);

public sealed record UpiQrSession(
    string ProviderOrderId,
    string ProviderPaymentId,
    string UpiIntentUrl,
    string? DisplayVpa = null);

public sealed record UpiPaymentSnapshot(
    string Id,
    string Status,
    long AmountMinor,
    string Currency,
    string? OrderId,
    IReadOnlyDictionary<string, string> Notes);

public sealed record UpiRefundResult(
    bool Success,
    string? RefundId,
    string Status,
    string? ErrorMessage);

/// <summary>Razorpay UPI S2S intent. Method name kept as CreateQrSessionAsync.</summary>
public interface IUpiCheckoutService
{
    Task<UpiQrSession> CreateQrSessionAsync(
        UpiQrSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<UpiPaymentSnapshot> GetPaymentAsync(
        string providerPaymentId,
        CancellationToken cancellationToken = default);

    Task<UpiRefundResult> RefundAsync(
        string providerPaymentId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default);

    bool MatchesExpected(UpiPaymentSnapshot snapshot, PaymentCaptureCheck expected);
}
