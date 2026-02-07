using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ParkingApp.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for real-time notifications.
/// Implements secure user-specific groups for targeted messaging.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects. Automatically joins user to their personal group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            // Add user to their personal notification group
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId.Value));
            _logger.LogInformation("User {UserId} connected with ConnectionId {ConnectionId}", userId, Context.ConnectionId);
        }
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects. Cleanup handled automatically by SignalR.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            _logger.LogInformation("User {UserId} disconnected. Exception: {Exception}", 
                userId, exception?.Message ?? "None");
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client method to acknowledge receipt of notification.
    /// </summary>
    public async Task AcknowledgeNotification(string notificationId)
    {
        var userId = GetUserId();
        _logger.LogDebug("User {UserId} acknowledged notification {NotificationId}", userId, notificationId);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the group name for a specific user.
    /// </summary>
    public static string GetUserGroupName(Guid userId) => $"user_{userId}";

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? Context.User?.FindFirst("sub")?.Value;
        
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
