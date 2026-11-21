using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Application.Interfaces;

public enum PaymentCaptureOutcome
{
    Applied = 0,
    AlreadyCaptured = 1,
    CannotAccept = 2,
    AlreadyCapturedDifferentId = 3,
    PurposeMismatch = 4,
    BookingNotFound = 5,
    Failed = 6
}

public sealed record PaymentCaptureCommand(
    Guid BookingId,
    string Purpose,
    string ProviderPaymentId,
    string? ProviderOrderId,
    decimal Amount,
    string Currency,
    string PaymentGateway,
    PaymentMethod PaymentMethod);

public sealed record PaymentCaptureResult(
    PaymentCaptureOutcome Outcome,
    string? Message,
    string? TransactionId,
    PaymentStatus Status,
    string? ReceiptUrl)
{
    public bool Success => Outcome is PaymentCaptureOutcome.Applied or PaymentCaptureOutcome.AlreadyCaptured;

    /// <summary>(b) CannotAccept and (c) AlreadyCapturedDifferentId are refundable. (a) never refunds.</summary>
    public bool NeedsRefund =>
        Outcome is PaymentCaptureOutcome.CannotAccept
            or PaymentCaptureOutcome.AlreadyCapturedDifferentId;

    public static PaymentCaptureResult Applied(string transactionId, string message, string? receiptUrl = null) =>
        new(PaymentCaptureOutcome.Applied, message, transactionId, PaymentStatus.Completed, receiptUrl);

    public static PaymentCaptureResult AlreadyCaptured(string transactionId, string message, string? receiptUrl = null) =>
        new(PaymentCaptureOutcome.AlreadyCaptured, message, transactionId, PaymentStatus.Completed, receiptUrl);

    public static PaymentCaptureResult CannotAccept(string message, string? transactionId = null) =>
        new(PaymentCaptureOutcome.CannotAccept, message, transactionId, PaymentStatus.Completed, null);

    public static PaymentCaptureResult AlreadyCapturedDifferentId(string message, string? recordedId = null) =>
        new(PaymentCaptureOutcome.AlreadyCapturedDifferentId, message, recordedId, PaymentStatus.Completed, null);

    public static PaymentCaptureResult PurposeMismatch(string message) =>
        new(PaymentCaptureOutcome.PurposeMismatch, message, null, PaymentStatus.Failed, null);

    public static PaymentCaptureResult BookingNotFound() =>
        new(PaymentCaptureOutcome.BookingNotFound, "Booking not found", null, PaymentStatus.Failed, null);

    public static PaymentCaptureResult Failed(string message) =>
        new(PaymentCaptureOutcome.Failed, message, null, PaymentStatus.Failed, null);
}

/// <summary>
/// Captures a matched provider charge against a booking, keyed by
/// (BookingId, Purpose, ProviderPaymentId). Never "any completed Payment".
/// </summary>
public interface IPaymentCaptureOrchestrator
{
    Task<PaymentCaptureResult> CaptureAsync(PaymentCaptureCommand command, CancellationToken cancellationToken = default);
}
