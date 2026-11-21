namespace ParkingApp.Marketplace.Application.Interfaces;

public sealed record CardCheckoutSession(string PaymentIntentId, string ClientSecret);

/// <summary>Stripe PaymentIntent create that returns both the PI id (persisted) and client secret (HTTP).</summary>
public interface IStripeCheckoutService
{
    Task<CardCheckoutSession> CreateCardSessionAsync(
        decimal amount,
        string currency = "INR",
        Dictionary<string, string>? notes = null,
        CancellationToken cancellationToken = default);
}
