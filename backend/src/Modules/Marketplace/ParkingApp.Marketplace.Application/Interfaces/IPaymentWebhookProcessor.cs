namespace ParkingApp.Marketplace.Application.Interfaces;

public sealed record PaymentWebhookProcessResult(
    int StatusCode,
    object Body,
    string? RetryAfter = null);

public interface IPaymentWebhookProcessor
{
    Task<PaymentWebhookProcessResult> ProcessRazorpayAsync(
        byte[] rawBody,
        string payloadSha256,
        string? eventIdHeader,
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default);
}
