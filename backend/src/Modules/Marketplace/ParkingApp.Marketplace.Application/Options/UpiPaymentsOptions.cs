namespace ParkingApp.Marketplace.Application.Options;

public sealed class UpiPaymentsOptions
{
    public const string SectionName = "Payments:Upi";

    public bool Enabled { get; set; } = false;

    public string Provider { get; set; } = "Razorpay";

    public string KeyId { get; set; } = string.Empty;

    public string KeySecret { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public int QrTtlSeconds { get; set; } = 900;

    public int AwaitingPaymentGraceMinutes { get; set; } = 30;

    public int CreateOrderPerMinute { get; set; } = 10;
}
