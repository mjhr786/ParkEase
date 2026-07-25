namespace ParkingApp.Application.Contracts.Notifications;

/// <summary>
/// Notifications module contract: business modules request delivery without knowing SignalR/Firebase/SMS details.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(Guid userId, NotificationSendRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stable request shape for cross-module notification delivery.
/// Channel names are free-form flags encoded as strings for contract isolation.
/// </summary>
public sealed record NotificationSendRequest(
    string Type,
    string Title,
    string Message,
    IReadOnlyList<string>? Channels = null,
    IReadOnlyDictionary<string, string>? Data = null,
    string Priority = "Normal");
