using Microsoft.Extensions.Logging;
using ParkingApp.Marketplace.Application.Interfaces;

namespace ParkingApp.Marketplace.Application.Services;

/// <summary>
/// Named Serilog events from the dual-rail design (KD-PAY-24). Message templates stay stable for alerts.
/// </summary>
internal static class PaymentObservability
{
    public const string MatcherMismatch = "PaymentCaptureMatcherMismatch";
    public const string NeedsRefund = "PaymentNeedsRefund";
    public const string RefundFailed = "PaymentRefundFailed";
    public const string RefundPermanentFailure = "PaymentRefundPermanentFailure";
    public const string ExpireJobCompleted = "ExpireJobCompleted";

    public const string ReasonMismatch = "mismatch";
    public const string ReasonCannotConfirm = "cannot_confirm";
    public const string ReasonOtherCharge = "other_charge";
    public const string ReasonFlagOff = "flag_off";

    public static long ToMinorUnits(decimal amount) =>
        (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    public static string CaptureRefundReason(PaymentCaptureOutcome outcome) =>
        outcome is PaymentCaptureOutcome.AlreadyCapturedDifferentId
            ? ReasonOtherCharge
            : ReasonCannotConfirm;

    public static void LogMatcherMismatch(
        ILogger logger,
        Guid bookingId,
        string? purpose,
        long expectedMinor,
        long actualMinor,
        string? providerPaymentId)
    {
        logger.LogWarning(
            "{Event} BookingId={BookingId} Purpose={Purpose} ExpectedMinor={ExpectedMinor} ActualMinor={ActualMinor} ProviderPaymentId={ProviderPaymentId}",
            MatcherMismatch,
            bookingId,
            purpose,
            expectedMinor,
            actualMinor,
            providerPaymentId);
    }

    public static void LogNeedsRefund(
        ILogger logger,
        Guid? bookingId,
        string? purpose,
        string? providerPaymentId,
        string reason)
    {
        logger.LogWarning(
            "{Event} BookingId={BookingId} Purpose={Purpose} ProviderPaymentId={ProviderPaymentId} Reason={Reason}",
            NeedsRefund,
            bookingId,
            purpose,
            providerPaymentId,
            reason);
    }

    public static void LogRefundFailed(ILogger logger, string? providerPaymentId, Exception? exception = null)
    {
        if (exception is null)
        {
            logger.LogWarning("{Event} ProviderPaymentId={ProviderPaymentId}", RefundFailed, providerPaymentId);
            return;
        }

        logger.LogWarning(
            exception,
            "{Event} ProviderPaymentId={ProviderPaymentId}",
            RefundFailed,
            providerPaymentId);
    }

    public static void LogRefundPermanentFailure(ILogger logger, string? providerPaymentId, int attempts)
    {
        logger.LogError(
            "{Event} ProviderPaymentId={ProviderPaymentId} Attempts={Attempts}",
            RefundPermanentFailure,
            providerPaymentId,
            attempts);
    }

    public static void LogExpireJobCompleted(
        ILogger logger,
        int expiredBookings,
        int expiredOrders,
        int needsRefundDrained,
        long durationMs)
    {
        logger.LogInformation(
            "{Event} ExpiredBookings={ExpiredBookings} ExpiredOrders={ExpiredOrders} NeedsRefundDrained={NeedsRefundDrained} DurationMs={DurationMs}",
            ExpireJobCompleted,
            expiredBookings,
            expiredOrders,
            needsRefundDrained,
            durationMs);
    }

    /// <summary>
    /// Server logs must never include the full intent URI (merchant <c>pa=</c> is noisy).
    /// </summary>
    public static string RedactIntentUrl(string? intentUrl)
    {
        if (string.IsNullOrWhiteSpace(intentUrl))
            return "upi://pay?pa=***";

        string? amount = null;
        var query = intentUrl;
        var q = intentUrl.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
            query = intentUrl[(q + 1)..];

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            if (!string.Equals(part[..eq], "am", StringComparison.OrdinalIgnoreCase))
                continue;
            amount = Uri.UnescapeDataString(part[(eq + 1)..]);
            break;
        }

        return string.IsNullOrWhiteSpace(amount)
            ? "upi://pay?pa=***"
            : $"upi://pay?pa=***&am={amount}";
    }
}
