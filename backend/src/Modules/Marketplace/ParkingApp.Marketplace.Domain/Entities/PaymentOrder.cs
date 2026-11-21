using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Domain.Entities;

public class PaymentOrder : BaseEntity
{
    public const string RailStripe = "stripe";
    public const string RailUpi = "upi";
    public const string ProviderStripe = "Stripe";
    public const string ProviderRazorpay = "Razorpay";

    public Guid BookingId { get; internal set; }
    public string Purpose { get; internal set; } = string.Empty;
    public string Rail { get; internal set; } = string.Empty;
    public string Provider { get; internal set; } = string.Empty;
    public string ProviderOrderId { get; internal set; } = string.Empty;
    public string? ProviderPaymentId { get; internal set; }
    public decimal Amount { get; internal set; }
    public string Currency { get; internal set; } = "INR";
    public PaymentOrderStatus Status { get; internal set; } = PaymentOrderStatus.Created;
    public DateTime? ExpiresAtUtc { get; internal set; }
    public DateTime? CapturedAtUtc { get; internal set; }
    public string? MetadataJson { get; internal set; }

    public virtual Booking Booking { get; internal set; } = null!;

    public bool IsLive => Status is PaymentOrderStatus.Created or PaymentOrderStatus.Pending;

    internal PaymentOrder()
    {
    }

    public static PaymentOrder Create(
        Guid bookingId,
        string purpose,
        string rail,
        string provider,
        string providerOrderId,
        decimal amount,
        string currency,
        PaymentOrderStatus status,
        string? providerPaymentId = null,
        DateTime? expiresAtUtc = null,
        string? metadataJson = null)
    {
        return new PaymentOrder
        {
            BookingId = bookingId,
            Purpose = purpose,
            Rail = rail,
            Provider = provider,
            ProviderOrderId = providerOrderId,
            ProviderPaymentId = providerPaymentId,
            Amount = amount,
            Currency = currency,
            Status = status,
            ExpiresAtUtc = expiresAtUtc,
            MetadataJson = metadataJson
        };
    }

    public void Cancel()
    {
        if (!IsLive)
            return;
        Status = PaymentOrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        if (!IsLive)
            return;
        Status = PaymentOrderStatus.Expired;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCaptured(string providerPaymentId, DateTime capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(providerPaymentId))
            throw new ValidationException("providerPaymentId", "Provider payment id is required");

        ProviderPaymentId = providerPaymentId.Trim();
        Status = PaymentOrderStatus.Captured;
        CapturedAtUtc = capturedAtUtc;
        UpdatedAt = capturedAtUtc;
    }
}
