namespace ParkingApp.Notifications.Contracts;

/// <summary>
/// Schedules push delivery off the request path. Best-effort; failures are logged, not thrown.
/// Use for chat and other latency-sensitive flows where in-app SignalR already covers online users.
/// </summary>
public interface IDeferredPushNotificationService
{
    /// <summary>
    /// Enqueues a user push and returns immediately (does not await FCM).
    /// </summary>
    void ScheduleSendToUser(Guid userId, PushNotificationPayload payload);
}
