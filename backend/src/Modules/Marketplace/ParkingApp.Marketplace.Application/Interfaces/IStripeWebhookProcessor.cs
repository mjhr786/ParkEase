namespace ParkingApp.Marketplace.Application.Interfaces;

public interface IStripeWebhookProcessor
{
    Task<PaymentWebhookProcessResult> ProcessAsync(
        byte[] rawBody,
        string? stripeSignatureHeader,
        DateTime startedAtUtc,
        CancellationToken cancellationToken = default);
}
