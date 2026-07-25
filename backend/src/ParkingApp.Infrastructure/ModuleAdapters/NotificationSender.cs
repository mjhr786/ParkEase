using ParkingApp.Notifications.Contracts;
using ParkingApp.Application.Contracts.Notifications;

namespace ParkingApp.Infrastructure.ModuleAdapters;

/// <summary>
/// Adapts Notifications contracts to the existing coordinator implementation.
/// </summary>
public sealed class NotificationSender : INotificationSender
{
    private readonly ParkingApp.Notifications.Contracts.INotificationCoordinator _coordinator;

    public NotificationSender(ParkingApp.Notifications.Contracts.INotificationCoordinator coordinator) => _coordinator = coordinator;

    public Task SendAsync(Guid userId, NotificationSendRequest request, CancellationToken cancellationToken = default)
    {
        var channels = MapChannels(request.Channels);
        var priority = MapPriority(request.Priority);

        var mapped = new NotificationRequest(
            request.Type,
            request.Title,
            request.Message,
            channels,
            request.Data is null ? null : new Dictionary<string, string>(request.Data),
            priority);

        return _coordinator.SendAsync(userId, mapped, cancellationToken);
    }

    private static NotificationChannels MapChannels(IReadOnlyList<string>? channels)
    {
        if (channels is null || channels.Count == 0)
            return NotificationChannels.All;

        var result = NotificationChannels.None;
        foreach (var channel in channels)
        {
            if (Enum.TryParse<NotificationChannels>(channel, ignoreCase: true, out var parsed))
                result |= parsed;
        }

        return result == NotificationChannels.None ? NotificationChannels.All : result;
    }

    private static NotificationPriority MapPriority(string priority) =>
        Enum.TryParse<NotificationPriority>(priority, ignoreCase: true, out var parsed)
            ? parsed
            : NotificationPriority.Normal;
}

