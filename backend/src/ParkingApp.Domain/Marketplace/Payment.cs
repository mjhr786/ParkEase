using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Shared;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Domain.Marketplace;

/// <summary>
/// Payment aggregate for booking charges and refunds.
/// Charge is a <see cref="Money"/> value object (persisted as Amount + Currency).
/// </summary>
public class Payment : BaseEntity
{
    public Guid BookingId { get; internal set; }
    public Guid UserId { get; internal set; }

    /// <summary>Principal charge amount and currency (owned / mapped to Amount + Currency columns).</summary>
    public Money Charge { get; internal set; } = Money.Zero();

    /// <summary>Convenience for call sites / tests (backed by Charge).</summary>
    public decimal Amount
    {
        get => Charge.Amount;
        internal set => Charge = new Money(value, Charge.Currency);
    }

    /// <summary>Convenience for call sites / tests (backed by Charge).</summary>
    public string Currency
    {
        get => Charge.Currency;
        internal set => Charge = new Money(Charge.Amount, value);
    }

    public PaymentMethod PaymentMethod { get; internal set; }
    public PaymentStatus Status { get; internal set; } = PaymentStatus.Pending;

    public string? TransactionId { get; internal set; }
    public string? PaymentGatewayReference { get; internal set; }
    public string? PaymentGateway { get; internal set; }

    public DateTime? PaidAt { get; internal set; }
    public DateTime? RefundedAt { get; internal set; }

    public decimal? RefundAmount { get; internal set; }
    public string? RefundReason { get; internal set; }
    public string? RefundTransactionId { get; internal set; }

    public string? ReceiptUrl { get; internal set; }
    public string? InvoiceNumber { get; internal set; }

    public string? FailureReason { get; internal set; }
    public string? Metadata { get; internal set; }

    public virtual Booking Booking { get; internal set; } = null!;
    public virtual User User { get; internal set; } = null!;

    internal Payment()
    {
    }

    public static Payment CreatePending(
        Guid bookingId,
        Guid userId,
        decimal amount,
        PaymentMethod paymentMethod,
        string currency = "INR")
    {
        if (bookingId == Guid.Empty)
            throw new ValidationException("bookingId", "Booking ID is required");
        if (userId == Guid.Empty)
            throw new ValidationException("userId", "User ID is required");

        Money charge;
        try
        {
            charge = new Money(amount, currency);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException("amount", ex.Message);
        }

        return new Payment
        {
            BookingId = bookingId,
            UserId = userId,
            Charge = charge,
            PaymentMethod = paymentMethod,
            Status = PaymentStatus.Pending
        };
    }

    public void ApplyGatewayResult(
        PaymentStatus status,
        string? transactionId,
        string? paymentGatewayReference,
        string paymentGateway,
        string? receiptUrl = null,
        string? failureReason = null,
        string? invoiceNumber = null)
    {
        if (Status == PaymentStatus.Completed && status != PaymentStatus.Completed)
            throw new BusinessRuleException("Payment.ApplyGatewayResult", "Cannot overwrite a completed payment with a non-completed status");

        Status = status;
        TransactionId = transactionId;
        PaymentGatewayReference = paymentGatewayReference;
        PaymentGateway = paymentGateway;
        ReceiptUrl = receiptUrl;

        if (status == PaymentStatus.Completed)
        {
            PaidAt = DateTime.UtcNow;
            InvoiceNumber = invoiceNumber ?? GenerateInvoiceNumber();
            FailureReason = null;
        }
        else if (status == PaymentStatus.Failed)
        {
            FailureReason = failureReason;
        }
        else if (status == PaymentStatus.Pending)
        {
            FailureReason = failureReason;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkSucceeded(
        string? transactionId,
        string? paymentGatewayReference,
        string paymentGateway,
        decimal? amount = null,
        string? receiptUrl = null,
        string? invoiceNumber = null)
    {
        if (amount.HasValue)
        {
            try
            {
                Charge = Charge.WithAmount(amount.Value);
            }
            catch (ArgumentException ex)
            {
                throw new ValidationException("amount", ex.Message);
            }
        }

        ApplyGatewayResult(
            PaymentStatus.Completed,
            transactionId,
            paymentGatewayReference,
            paymentGateway,
            receiptUrl,
            failureReason: null,
            invoiceNumber);
    }

    public void MarkFailed(string? errorMessage, string? paymentGateway = null)
    {
        if (Status == PaymentStatus.Completed)
            throw new BusinessRuleException("Payment.MarkFailed", "Cannot fail a completed payment");

        Status = PaymentStatus.Failed;
        FailureReason = errorMessage;
        if (!string.IsNullOrWhiteSpace(paymentGateway))
            PaymentGateway = paymentGateway;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordRefund(decimal refundedAmount, string reason, string? refundTransactionId)
    {
        if (Status is not (PaymentStatus.Completed or PaymentStatus.PartialRefund))
            throw new BusinessRuleException("Payment.RecordRefund", "Can only refund completed or partially refunded payments");

        Money refund;
        try
        {
            refund = new Money(refundedAmount, Charge.Currency);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException("refundedAmount", ex.Message);
        }

        if (refund.Amount <= 0)
            throw new ValidationException("refundedAmount", "Refund amount must be positive");

        var totalRefunded = (RefundAmount ?? 0) + refund.Amount;
        if (totalRefunded > Charge.Amount)
            throw new BusinessRuleException("Payment.RecordRefund", "Refund amount exceeds payment amount");

        RefundAmount = totalRefunded;
        RefundReason = reason;
        RefundTransactionId = refundTransactionId;
        RefundedAt = DateTime.UtcNow;
        Status = totalRefunded >= Charge.Amount ? PaymentStatus.Refunded : PaymentStatus.PartialRefund;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string GenerateInvoiceNumber() =>
        $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
