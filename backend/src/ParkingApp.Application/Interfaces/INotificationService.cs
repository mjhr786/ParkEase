namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Service interface for sending real-time notifications to users.
/// Follows DDD principle of keeping infrastructure concerns abstracted.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification to a specific user.
    /// </summary>
    Task NotifyUserAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a notification to multiple users.
    /// </summary>
    Task NotifyUsersAsync(IEnumerable<Guid> userIds, NotificationDto notification, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a notification to all connected clients (broadcast).
    /// </summary>
    Task BroadcastAsync(NotificationDto notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Notification data transfer object for real-time messaging.
/// </summary>
public record NotificationDto(
    string Type,
    string Title,
    string Message,
    object? Data = null
)
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Predefined notification types for type-safe event handling.
/// </summary>
public static class NotificationTypes
{
    // Booking workflow notifications
    public const string BookingRequested = "booking.requested";
    public const string BookingApproved = "booking.approved";
    public const string BookingRejected = "booking.rejected";
    public const string BookingCancelled = "booking.cancelled";
    public const string BookingExpiringSoon = "booking.expiring";
    
    // Payment notifications
    public const string PaymentCompleted = "payment.completed";
    public const string PaymentFailed = "payment.failed";
    
    // Check-in/out notifications
    public const string CheckIn = "booking.checkin";
    public const string CheckOut = "booking.checkout";
    
    // General notifications
    public const string Info = "general.info";
    public const string Warning = "general.warning";
}
