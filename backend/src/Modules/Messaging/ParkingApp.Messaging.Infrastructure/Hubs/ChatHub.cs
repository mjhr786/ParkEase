using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS;
using ParkingApp.Messaging.Application.Commands.Chat;
using ParkingApp.Messaging.Application.Queries.Chat;
using ParkingApp.Messaging.Application.DTOs;
using System.Security.Claims;

namespace ParkingApp.Messaging.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for real-time chat messaging.
/// Clients join personal user groups on connect; optional conversation groups while viewing a thread.
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
    /// Join a conversation group after server-side participant check (for focused thread delivery).
    /// </summary>
    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var access = await _dispatcher.QueryAsync(new CanAccessConversationQuery(userId.Value, conversationId));
        if (!access.Success || access.Data != true)
        {
            await Clients.Caller.SendAsync("Error", access.Message ?? "Unauthorized");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));
        _logger.LogDebug("Chat: User {UserId} joined conversation {ConversationId}", userId, conversationId);
    }

    /// <summary>
    /// Leave a conversation group when navigating away from the thread.
    /// </summary>
    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));
    }

    /// <summary>
    /// Client-invokable method to send a message.
    /// Dispatches via CQRS and pushes real-time notification to the recipient.
    /// </summary>
    public async Task SendMessage(Guid parkingSpaceId, string content, Guid? conversationId = null)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var dto = new SendMessageDto(parkingSpaceId, content, conversationId);
        var result = await _dispatcher.SendAsync(
            new SendMessageCommand(userId.Value, dto));

        if (result.Success && result.Data != null)
        {
            await BroadcastReceiveMessageAsync(userId.Value, result.Data);
        }
        else
        {
            await Clients.Caller.SendAsync("Error", result.Message);
        }
    }

    /// <summary>
    /// Fan-out ReceiveMessage to user groups (badge / multi-device) and conversation group (active viewers).
    /// Clients must dedupe by message id (web already does).
    /// </summary>
    public static async Task BroadcastReceiveMessageAsync(
        IHubContext<ChatHub> hub,
        Guid senderId,
        ChatMessageDto message,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            hub.Clients.Group(GetUserGroupName(senderId))
                .SendAsync("ReceiveMessage", message, cancellationToken)
        };

        if (message.RecipientId is Guid recipientId && recipientId != senderId)
        {
            tasks.Add(hub.Clients.Group(GetUserGroupName(recipientId))
                .SendAsync("ReceiveMessage", message, cancellationToken));
        }

        tasks.Add(hub.Clients.Group(GetConversationGroupName(message.ConversationId))
            .SendAsync("ReceiveMessage", message, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private async Task BroadcastReceiveMessageAsync(Guid senderId, ChatMessageDto message)
    {
        var tasks = new List<Task>
        {
            Clients.Group(GetUserGroupName(senderId))
                .SendAsync("ReceiveMessage", message)
        };

        if (message.RecipientId is Guid recipientId && recipientId != senderId)
        {
            tasks.Add(Clients.Group(GetUserGroupName(recipientId))
                .SendAsync("ReceiveMessage", message));
        }

        tasks.Add(Clients.Group(GetConversationGroupName(message.ConversationId))
            .SendAsync("ReceiveMessage", message));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Gets the group name for a specific user's chat.
    /// </summary>
    public static string GetUserGroupName(Guid userId) => $"chat_user_{userId}";

    /// <summary>
    /// Group for clients currently viewing a conversation thread.
    /// </summary>
    public static string GetConversationGroupName(Guid conversationId) => $"chat_conv_{conversationId}";

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? Context.User?.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
