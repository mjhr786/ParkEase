using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Domain.Entities;

public class PaymentWebhookEvent : BaseEntity
{
    public string Provider { get; internal set; } = string.Empty;
    public string ProviderEventId { get; internal set; } = string.Empty;
    public PaymentWebhookEventStatus Status { get; internal set; } = PaymentWebhookEventStatus.Received;
    public string? Outcome { get; internal set; }
    public bool SignatureValid { get; internal set; }
    public string? PayloadSha256 { get; internal set; }
    public int RefundAttemptCount { get; internal set; }
    public DateTime? ProcessedAtUtc { get; internal set; }
    public string? ProviderPaymentId { get; internal set; }
    public decimal? Amount { get; internal set; }
    public string? Currency { get; internal set; }

    internal PaymentWebhookEvent()
    {
    }

    public static PaymentWebhookEvent Create(
        string provider,
        string providerEventId,
        bool signatureValid,
        string? payloadSha256)
    {
        return new PaymentWebhookEvent
        {
            Provider = provider,
            ProviderEventId = providerEventId,
            Status = PaymentWebhookEventStatus.Received,
            SignatureValid = signatureValid,
            PayloadSha256 = payloadSha256
        };
    }

    public void MarkProcessed(string outcome, DateTime utcNow)
    {
        Outcome = outcome;
        Status = PaymentWebhookEventStatus.Processed;
        ProcessedAtUtc = utcNow;
        UpdatedAt = utcNow;
    }

    public void SetOutcome(string outcome)
    {
        Outcome = outcome;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementRefundAttempt()
    {
        RefundAttemptCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRefundTarget(string providerPaymentId, decimal amount, string currency)
    {
        if (!string.IsNullOrWhiteSpace(providerPaymentId))
            ProviderPaymentId = providerPaymentId.Trim();
        Amount = amount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "INR" : currency;
        UpdatedAt = DateTime.UtcNow;
    }
}
