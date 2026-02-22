using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Chat;
using ParkingApp.Application.CQRS.Queries.Chat;
using ParkingApp.Application.DTOs;
using ParkingApp.Notifications.Hubs;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IHubContext<ChatHub> _chatHubContext;

    public ChatController(IDispatcher dispatcher, IHubContext<ChatHub> chatHubContext)
    {
        _dispatcher = dispatcher;
        _chatHubContext = chatHubContext;
    }

    /// <summary>
    /// Get all conversations for the current user (paginated).
    /// </summary>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(ApiResponse<ConversationListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(
            new GetConversationsQuery(userId.Value, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get messages for a specific conversation (paginated, newest first).
    /// </summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(ApiResponse<List<ChatMessageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(
        Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(
            new GetMessagesQuery(userId.Value, conversationId, page, pageSize), cancellationToken);

        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Send a message. Creates conversation if it doesn't exist. Pushes real-time via SignalR.
    /// </summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var result = await _dispatcher.SendAsync(
                new SendMessageCommand(userId.Value, dto), cancellationToken);

            if (!result.Success)
                return BadRequest(result);

            // Push real-time message to both participants via SignalR
            if (result.Data != null)
            {
                // Send to the sender's group (for multi-device sync)
                await _chatHubContext.Clients
                    .Group(ChatHub.GetUserGroupName(userId.Value))
                    .SendAsync("ReceiveMessage", result.Data, cancellationToken);

                // Determine and notify the other participant
                var getConversations = await _dispatcher.QueryAsync(
                    new GetConversationsQuery(userId.Value, 1, 100), cancellationToken);

                var conv = getConversations.Data?.Conversations
                    .FirstOrDefault(c => c.Id == result.Data.ConversationId);

                if (conv != null)
                {
                    await _chatHubContext.Clients
                        .Group(ChatHub.GetUserGroupName(conv.OtherParticipantId))
                        .SendAsync("ReceiveMessage", result.Data, cancellationToken);
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<ChatMessageDto>(false, $"Internal error: {ex.Message}", null));
        }
    }

    /// <summary>
    /// Mark all messages in a conversation as read for the current user.
    /// </summary>
    [HttpPost("conversations/{conversationId:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsRead(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new MarkMessagesReadCommand(userId.Value, conversationId), cancellationToken);

        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);

        // Notify the OTHER participant that their messages were read
        var getConversations = await _dispatcher.QueryAsync(
            new GetConversationsQuery(userId.Value, 1, 100), cancellationToken);

        var conv = getConversations.Data?.Conversations
            .FirstOrDefault(c => c.Id == conversationId);

        if (conv != null)
        {
            await _chatHubContext.Clients
                .Group(ChatHub.GetUserGroupName(conv.OtherParticipantId))
                .SendAsync("MessagesRead", conversationId, cancellationToken);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get total unread message count for the current user.
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var count = await _dispatcher.QueryAsync(
                new GetConversationsQuery(userId.Value, 1, 100), cancellationToken);

            var totalUnread = count.Data?.Conversations?.Sum(c => c.UnreadCount) ?? 0;
            return Ok(new ApiResponse<int>(true, null, totalUnread));
        }
        catch
        {
            return Ok(new ApiResponse<int>(true, null, 0));
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
