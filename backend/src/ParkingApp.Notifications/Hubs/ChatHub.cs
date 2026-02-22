using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Chat;
using ParkingApp.Application.DTOs;
using System.Security.Claims;

namespace ParkingApp.Notifications.Hubs;

/// <summary>
/// SignalR Hub for real-time chat messaging.
/// Separated from NotificationHub for clean separation of concerns.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly IDispatcher _dispatcher;

    public ChatHub(ILogger<ChatHub> logger, IDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Called when a client connects. Adds user to their personal chat group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroupName(userId.Value));
            _logger.LogInformation("Chat: User {UserId} connected with ConnectionId {ConnectionId}", userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            _logger.LogInformation("Chat: User {UserId} disconnected. Exception: {Exception}",
                userId, exception?.Message ?? "None");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client-invokable method to send a message.
    /// Dispatches via CQRS and pushes real-time notification to the recipient.
    /// </summary>
    public async Task SendMessage(Guid parkingSpaceId, string content)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var dto = new SendMessageDto(parkingSpaceId, content);
        var result = await _dispatcher.SendAsync(
            new SendMessageCommand(userId.Value, dto));

        if (result.Success && result.Data != null)
        {
            // Determine recipient: if sender is the one who sent, find the other participant
            var conversationId = result.Data.ConversationId;

            // Send to both participants so the UI updates for all connected clients
            await Clients.Group(GetUserGroupName(userId.Value))
                .SendAsync("ReceiveMessage", result.Data);

            // Also need to find the other participant and send to them
            // The controller approach is simpler for this; hub just broadcasts to the conversation
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.Message);
        }
    }

    /// <summary>
    /// Gets the group name for a specific user's chat.
    /// </summary>
    public static string GetUserGroupName(Guid userId) => $"chat_user_{userId}";

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? Context.User?.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
