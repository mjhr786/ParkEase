using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ParkingApp.Application.CQRS;
using ParkingApp.Messaging.Application;
using ParkingApp.Messaging.Application.Commands.Chat;
using ParkingApp.Messaging.Application.Queries.Chat;
using ParkingApp.Application.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Messaging.Infrastructure.Hubs;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IDispatcher dispatcher,
        IHubContext<ChatHub> chatHubContext,
        ILogger<ChatController> logger)
    {
        _dispatcher = dispatcher;
        _chatHubContext = chatHubContext;
        _logger = logger;
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

        // Clamp early so logs/metrics reflect effective work (handler also clamps defensively).
        (page, pageSize) = ChatPaging.ClampConversations(page, pageSize);

        var sw = Stopwatch.StartNew();
        var result = await _dispatcher.QueryAsync(
            new GetConversationsQuery(userId.Value, page, pageSize), cancellationToken);
        sw.Stop();
        _logger.LogInformation(
            "Chat.GetConversations user={UserId} page={Page} pageSize={PageSize} elapsedMs={ElapsedMs:0.0} count={Count}",
            userId, page, pageSize, sw.Elapsed.TotalMilliseconds,
            result.Data?.Conversations?.Count ?? 0);

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

        (page, pageSize) = ChatPaging.ClampMessages(page, pageSize);

        var sw = Stopwatch.StartNew();
        var result = await _dispatcher.QueryAsync(
            new GetMessagesQuery(userId.Value, conversationId, page, pageSize), cancellationToken);
        sw.Stop();
        _logger.LogInformation(
            "Chat.GetMessages user={UserId} conversation={ConversationId} elapsedMs={ElapsedMs:0.0} count={Count}",
            userId, conversationId, sw.Elapsed.TotalMilliseconds, result.Data?.Count ?? 0);

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
            var sw = Stopwatch.StartNew();
            var result = await _dispatcher.SendAsync(
                new SendMessageCommand(userId.Value, dto), cancellationToken);

            if (!result.Success)
                return BadRequest(result);

            if (result.Data != null)
            {
                await ChatHub.BroadcastReceiveMessageAsync(
                    _chatHubContext, userId.Value, result.Data, cancellationToken);
            }

            sw.Stop();
            _logger.LogInformation(
                "Chat.SendMessage user={UserId} conversation={ConversationId} elapsedMs={ElapsedMs:0.0} success={Success}",
                userId, result.Data?.ConversationId, sw.Elapsed.TotalMilliseconds, result.Success);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat.SendMessage failed for user {UserId}", userId);
            return StatusCode(500, new ApiResponse<ChatMessageDto>(false, $"Internal error: {ex.Message}", null));
        }
    }

    /// <summary>
    /// Mark all messages in a conversation as read for the current user.
    /// </summary>
    [HttpPost("conversations/{conversationId:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<MarkMessagesReadResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsRead(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var sw = Stopwatch.StartNew();
        var result = await _dispatcher.SendAsync(
            new MarkMessagesReadCommand(userId.Value, conversationId), cancellationToken);

        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);

        // Notify the other participant (user group) + anyone viewing the conversation
        if (result.Data is { OtherParticipantId: var otherId } && otherId != userId.Value)
        {
            await Task.WhenAll(
                _chatHubContext.Clients
                    .Group(ChatHub.GetUserGroupName(otherId))
                    .SendAsync("MessagesRead", conversationId, cancellationToken),
                _chatHubContext.Clients
                    .Group(ChatHub.GetConversationGroupName(conversationId))
                    .SendAsync("MessagesRead", conversationId, cancellationToken));
        }

        sw.Stop();
        _logger.LogInformation(
            "Chat.MarkAsRead user={UserId} conversation={ConversationId} elapsedMs={ElapsedMs:0.0}",
            userId, conversationId, sw.Elapsed.TotalMilliseconds);

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

        var result = await _dispatcher.QueryAsync(
            new GetUnreadMessageCountQuery(userId.Value), cancellationToken);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
