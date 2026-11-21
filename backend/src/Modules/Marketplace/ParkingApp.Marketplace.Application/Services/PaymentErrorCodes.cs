namespace ParkingApp.Marketplace.Application.Services;

public static class PaymentErrorCodes
{
    public const string UpiDisabled = "upi_disabled";
    public const string UpiContactRequired = "upi_contact_required";
    public const string UpiContactInvalid = "upi_contact_invalid";
    public const string UpiIpRequired = "upi_ip_required";
    public const string PaymentInProgress = "payment_in_progress";
    public const string RailMismatch = "rail_mismatch";
}

internal static class PaymentLock
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan WebhookTtl = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan ReplayMinRemaining = TimeSpan.FromSeconds(120);
    public static readonly TimeSpan WebhookGetTimeout = TimeSpan.FromSeconds(2);

    public static string Key(Guid bookingId, string purpose) =>
        $"marketplace:payment:{bookingId:N}:{purpose}";

    public const string ExpireAwaitingJob = "job:expire-awaiting-payments";
    public static readonly TimeSpan ExpireJobTtl = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan ErrorDrainAge = TimeSpan.FromMinutes(2);
}

internal static class PaymentWebhookOutcomes
{
    public const string Captured = "captured";
    public const string CapturedDuplicate = "captured_duplicate";
    public const string CapturedRevived = "captured_revived";
    public const string IgnoredUnhandled = "ignored_unhandled";
    public const string IgnoredNotCaptured = "ignored_not_captured";
    public const string IgnoredFailed = "ignored_failed";
    public const string RejectedMismatch = "rejected_mismatch";
    public const string NeedsRefund = "needs_refund";
    public const string Refunded = "refunded";
    public const string RefundFailedPermanent = "refund_failed_permanent";
    public const string Error = "error";

    public const int MaxRefundAttempts = 5;
    public const string ProviderRazorpay = "razorpay";
}

internal static class UniqueConstraint
{
    public static bool IsViolation(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            var typeName = current.GetType().FullName ?? string.Empty;
            if (typeName.Contains("PostgresException", StringComparison.Ordinal))
            {
                var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
                if (sqlState == "23505")
                    return true;
            }

            var message = current.Message ?? string.Empty;
            if (message.Contains("23505", StringComparison.Ordinal)
                || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
