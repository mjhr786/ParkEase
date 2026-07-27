using ParkingApp.Notifications.Contracts;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Messaging.Domain.Interfaces;
using ParkingApp.Messaging.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Messaging.Application.Commands.Chat;

// ────────────────────────────────────────────────────────────────────────────
// Commands
// ────────────────────────────────────────────────────────────────────────────

public sealed record SendMessageCommand(Guid SenderId, SendMessageDto Dto) : ICommand<ApiResponse<ChatMessageDto>>;
public sealed record MarkMessagesReadCommand(Guid UserId, Guid ConversationId) : ICommand<ApiResponse<MarkMessagesReadResult>>;

// ────────────────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────────────────

internal sealed class SendMessageHandler : ICommandHandler<SendMessageCommand, ApiResponse<ChatMessageDto>>
{
    private readonly IMessagingUnitOfWork _messaging;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly ILogger<SendMessageHandler> _logger;
    private readonly IDeferredPushNotificationService _deferredPush;
    private readonly ICacheService _cache;

    public SendMessageHandler(
        IMessagingUnitOfWork messaging,
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        ILogger<SendMessageHandler> logger,
        IDeferredPushNotificationService deferredPush,
        ICacheService cache)
    {
        _messaging = messaging;
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _logger = logger;
        _deferredPush = deferredPush;
        _cache = cache;
    }

    public async Task<ApiResponse<ChatMessageDto>> HandleAsync(SendMessageCommand command, CancellationToken cancellationToken = default)
    {
        // Validate content
        var content = command.Dto.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return new ApiResponse<ChatMessageDto>(false, "Message content cannot be empty", null);

        if (content.Length > 2000)
            return new ApiResponse<ChatMessageDto>(false, "Message content cannot exceed 2000 characters", null);

        Conversation? conversation = null;

        // Prefer conversation id path — skip parking-space lookup when continuing an existing thread
        if (command.Dto.ConversationId.HasValue)
        {
            conversation = await _messaging.Conversations.GetByIdAsync(command.Dto.ConversationId.Value, cancellationToken);

            if (conversation != null && conversation.UserId != command.SenderId && conversation.VendorId != command.SenderId)
                return new ApiResponse<ChatMessageDto>(false, "Unauthorized for this conversation", null);

            // Mismatched parkingSpaceId is ignored once conversation is authorized (keeps clients simple)
            if (conversation != null && conversation.ParkingSpaceId != command.Dto.ParkingSpaceId)
            {
                _logger.LogDebug(
                    "SendMessage parkingSpaceId {DtoSpace} differs from conversation {ConvSpace}; using conversation",
                    command.Dto.ParkingSpaceId, conversation.ParkingSpaceId);
            }
        }

        // Fallback: find or create by parking space + participants (mobile often omits conversationId)
        if (conversation == null)
        {
            // Renter path first — single unique index lookup (ParkingSpaceId, UserId)
            conversation = await _messaging.Conversations.GetByParticipantsAsync(
                command.Dto.ParkingSpaceId, command.SenderId, cancellationToken);

            // Vendor path: only when exactly one thread exists for this space+vendor (same semantics as before)
            if (conversation == null)
            {
                conversation = await _messaging.Conversations.GetSoleByVendorAndSpaceAsync(
                    command.Dto.ParkingSpaceId, command.SenderId, cancellationToken);
            }

            if (conversation == null)
            {
                var parkingSpace = await _parkingSpaceLookup.GetByIdAsync(command.Dto.ParkingSpaceId, cancellationToken);
                if (parkingSpace == null)
                    return new ApiResponse<ChatMessageDto>(false, "Parking space not found", null);

                // Only prevent self-chat when creating a NEW conversation
                if (parkingSpace.OwnerId == command.SenderId)
                    return new ApiResponse<ChatMessageDto>(false, "Cannot start a conversation with yourself", null);

                var now = DateTime.UtcNow;
                conversation = new Conversation
                {
                    ParkingSpaceId = command.Dto.ParkingSpaceId,
                    UserId = command.SenderId,
                    VendorId = parkingSpace.OwnerId,
                    LastMessageAt = now
                };
                await _messaging.Conversations.AddAsync(conversation, cancellationToken);
            }
        }

        // Create message
        var message = new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderId = command.SenderId,
            Content = content
        };

        await _messaging.ChatMessages.AddAsync(message, cancellationToken);

        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.LastMessagePreview = content.Length > 100 ? content[..100] : content;

        await _messaging.SaveChangesAsync(cancellationToken);

        var sender = await _userLookup.GetByIdAsync(command.SenderId, cancellationToken);
        var senderName = string.IsNullOrWhiteSpace(sender?.FullName) ? "Unknown" : sender.FullName;

        Guid recipientId = conversation.UserId == command.SenderId
            ? conversation.VendorId
            : conversation.UserId;

        // Badge counts must not serve stale cache after a write
        await InvalidateUnreadCachesAsync(command.SenderId, recipientId, cancellationToken);

        _logger.LogInformation(
            "Message sent in conversation {ConversationId} by user {SenderId}",
            conversation.Id, command.SenderId);

        // FCM must not block HTTP — online users already get SignalR from the controller/hub
        try
        {
            var payload = new PushNotificationPayload(
                Title: $"New message from {sender?.FirstName ?? "User"}",
                Body: content.Length > 100 ? content[..100] + "..." : content,
                Data: new Dictionary<string, string>
                {
                    { "type", "chat_message" },
                    { "conversationId", conversation.Id.ToString() }
                },
                Priority: PushPriority.High
            );
            _deferredPush.ScheduleSendToUser(recipientId, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to schedule chat push notification for recipient {RecipientId}", recipientId);
        }

        var dto = new ChatMessageDto(
            message.Id,
            message.ConversationId,
            message.SenderId,
            senderName,
            message.Content,
            message.IsRead,
            message.CreatedAt,
            recipientId);

        return new ApiResponse<ChatMessageDto>(true, "Message sent", dto);
    }

    private Task InvalidateUnreadCachesAsync(Guid userA, Guid userB, CancellationToken cancellationToken) =>
        Task.WhenAll(
            _cache.RemoveAsync(CacheKeys.ChatUnread(userA), cancellationToken),
            _cache.RemoveAsync(CacheKeys.ChatUnread(userB), cancellationToken));
}

internal sealed class MarkMessagesReadHandler : ICommandHandler<MarkMessagesReadCommand, ApiResponse<MarkMessagesReadResult>>
{
    private readonly IMessagingUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public MarkMessagesReadHandler(IMessagingUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<MarkMessagesReadResult>> HandleAsync(
        MarkMessagesReadCommand command,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _unitOfWork.Conversations.GetByIdAsync(command.ConversationId, cancellationToken);
        if (conversation == null)
            return new ApiResponse<MarkMessagesReadResult>(false, "Conversation not found", null);

        if (conversation.UserId != command.UserId && conversation.VendorId != command.UserId)
            return new ApiResponse<MarkMessagesReadResult>(false, "Unauthorized", null);

        // Relational path uses ExecuteUpdate (already persisted); InMemory needs SaveChanges.
        var requiresSave = await _unitOfWork.ChatMessages.MarkAsReadAsync(
            command.ConversationId, command.UserId, cancellationToken);
        if (requiresSave)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        var otherParticipantId = conversation.UserId == command.UserId
            ? conversation.VendorId
            : conversation.UserId;

        // Reader's badge cache is stale after mark-as-read
        await _cache.RemoveAsync(CacheKeys.ChatUnread(command.UserId), cancellationToken);

        return new ApiResponse<MarkMessagesReadResult>(
            true,
            "Messages marked as read",
            new MarkMessagesReadResult(true, otherParticipantId));
    }
}
