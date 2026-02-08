namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Service interface for sending SMS notifications.
/// Decoupled from specific SMS providers (Twilio, AWS SNS, etc.)
/// </summary>
public interface ISmsNotificationService
{
    /// <summary>
    /// Send an SMS to a single phone number.
    /// </summary>
    Task<SmsResult> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send SMS to multiple phone numbers.
    /// </summary>
    Task<IEnumerable<SmsResult>> SendBulkAsync(IEnumerable<string> phoneNumbers, string message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send a templated SMS message.
    /// </summary>
    Task<SmsResult> SendTemplatedAsync(string phoneNumber, string templateId, Dictionary<string, string> placeholders, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service interface for sending Push notifications.
/// Decoupled from specific providers (Firebase, APNs, OneSignal, etc.)
/// </summary>
public interface IPushNotificationService
{
    /// <summary>
    /// Send push notification to a specific device.
    /// </summary>
    Task<PushResult> SendToDeviceAsync(string deviceToken, PushNotificationPayload payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send push notification to a user (all their registered devices).
    /// </summary>
    Task<PushResult> SendToUserAsync(Guid userId, PushNotificationPayload payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send push notification to a topic/channel.
    /// </summary>
    Task<PushResult> SendToTopicAsync(string topic, PushNotificationPayload payload, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Subscribe a device to a topic.
    /// </summary>
    Task<bool> SubscribeToTopicAsync(string deviceToken, string topic, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Unsubscribe a device from a topic.
    /// </summary>
    Task<bool> UnsubscribeFromTopicAsync(string deviceToken, string topic, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unified notification coordinator that routes notifications through appropriate channels.
/// </summary>
public interface INotificationCoordinator
{
    /// <summary>
    /// Send notification to a user through all configured channels.
    /// </summary>
    Task SendAsync(Guid userId, NotificationRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Send notification to multiple users.
    /// </summary>
    Task SendBulkAsync(IEnumerable<Guid> userIds, NotificationRequest request, CancellationToken cancellationToken = default);
}

#region DTOs

/// <summary>
/// Result of an SMS send operation.
/// </summary>
public record SmsResult(
    bool Success,
    string? MessageId = null,
    string? ErrorMessage = null,
    SmsStatus Status = SmsStatus.Unknown
);

/// <summary>
/// Result of a push notification send operation.
/// </summary>
public record PushResult(
    bool Success,
    string? MessageId = null,
    string? ErrorMessage = null,
    int SuccessCount = 0,
    int FailureCount = 0
);

/// <summary>
/// Payload for push notifications.
/// </summary>
public record PushNotificationPayload(
    string Title,
    string Body,
    string? ImageUrl = null,
    Dictionary<string, string>? Data = null,
    PushPriority Priority = PushPriority.Normal,
    string? Sound = "default",
    int? Badge = null,
    string? ClickAction = null
);

/// <summary>
/// Unified notification request for the coordinator.
/// </summary>
public record NotificationRequest(
    string Type,
    string Title,
    string Message,
    NotificationChannels Channels = NotificationChannels.All,
    Dictionary<string, string>? Data = null,
    NotificationPriority Priority = NotificationPriority.Normal
);

/// <summary>
/// SMS delivery status.
/// </summary>
public enum SmsStatus
{
    Unknown,
    Queued,
    Sending,
    Sent,
    Delivered,
    Failed,
    Undeliverable
}

/// <summary>
/// Push notification priority levels.
/// </summary>
public enum PushPriority
{
    Low,
    Normal,
    High
}

/// <summary>
/// Notification priority levels.
/// </summary>
public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Notification delivery channels.
/// </summary>
[Flags]
public enum NotificationChannels
{
    None = 0,
    InApp = 1,        // SignalR real-time
    Push = 2,         // Mobile push notifications
    Sms = 4,          // SMS text messages
    Email = 8,        // Email notifications (future)
    All = InApp | Push | Sms
}

/// <summary>
/// Push notification topics for subscriptions.
/// </summary>
public static class PushTopics
{
    public const string BookingUpdates = "booking_updates";
    public const string PaymentUpdates = "payment_updates";
    public const string Promotions = "promotions";
    public const string SystemAlerts = "system_alerts";
    public const string VendorAlerts = "vendor_alerts";
}

#endregion
