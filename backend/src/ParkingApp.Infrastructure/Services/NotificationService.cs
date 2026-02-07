using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.Infrastructure.Hubs;

namespace ParkingApp.Infrastructure.Services;

/// <summary>
/// SignalR-based notification service implementation.
/// Thread-safe and optimized for high-throughput messaging.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;
    private const string ReceiveNotificationMethod = "ReceiveNotification";

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotifyUserAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = NotificationHub.GetUserGroupName(userId);
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(ReceiveNotificationMethod, notification, cancellationToken);
            
            _logger.LogDebug("Notification sent to user {UserId}: {Type} - {Title}", 
                userId, notification.Type, notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
            // Don't throw - notifications are non-critical
        }
    }

    /// <inheritdoc />
    public async Task NotifyUsersAsync(IEnumerable<Guid> userIds, NotificationDto notification, CancellationToken cancellationToken = default)
    {
        var tasks = userIds.Select(userId => NotifyUserAsync(userId, notification, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public async Task BroadcastAsync(NotificationDto notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All
                .SendAsync(ReceiveNotificationMethod, notification, cancellationToken);
            
            _logger.LogDebug("Broadcast notification sent: {Type} - {Title}", 
                notification.Type, notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast notification");
        }
    }
}
