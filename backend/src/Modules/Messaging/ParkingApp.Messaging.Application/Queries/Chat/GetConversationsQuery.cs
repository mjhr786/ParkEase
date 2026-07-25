using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Messaging.Application.Mappings;
using ParkingApp.Messaging.Domain.Interfaces;

namespace ParkingApp.Messaging.Application.Queries.Chat;

// ────────────────────────────────────────────────────────────────────────────
// Queries
// ────────────────────────────────────────────────────────────────────────────

public sealed record GetConversationsQuery(Guid UserId, int Page = 1, int PageSize = 20) : IQuery<ApiResponse<ConversationListDto>>;
public sealed record GetMessagesQuery(Guid UserId, Guid ConversationId, int Page = 1, int PageSize = 50) : IQuery<ApiResponse<List<ChatMessageDto>>>;
public sealed record GetUnreadMessageCountQuery(Guid UserId) : IQuery<ApiResponse<int>>;

/// <summary>
/// Lightweight participant check for SignalR JoinConversation (no DTO payload).
/// </summary>
public sealed record CanAccessConversationQuery(Guid UserId, Guid ConversationId) : IQuery<ApiResponse<bool>>;

// ────────────────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────────────────

internal sealed class GetConversationsHandler : IQueryHandler<GetConversationsQuery, ApiResponse<ConversationListDto>>
{
    private readonly IMessagingUnitOfWork _unitOfWork;
    private readonly IUserLookup _userLookup;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;

    public GetConversationsHandler(
        IMessagingUnitOfWork unitOfWork,
        IUserLookup userLookup,
        IParkingSpaceLookup parkingSpaceLookup)
    {
        _unitOfWork = unitOfWork;
        _userLookup = userLookup;
        _parkingSpaceLookup = parkingSpaceLookup;
    }

    public async Task<ApiResponse<ConversationListDto>> HandleAsync(
        GetConversationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var conversations = (await _unitOfWork.Conversations.GetByUserIdAsync(
            query.UserId, query.Page, query.PageSize, cancellationToken)).ToList();
        var totalCount = await _unitOfWork.Conversations.CountByUserIdAsync(query.UserId, cancellationToken);

        if (conversations.Count == 0)
        {
            return new ApiResponse<ConversationListDto>(
                true,
                null,
                new ConversationListDto(
                    new List<ConversationDto>(),
                    totalCount,
                    query.Page,
                    query.PageSize,
                    (int)Math.Ceiling(totalCount / (double)query.PageSize)));
        }

        var conversationIds = conversations.Select(c => c.Id).ToList();
        var otherIds = conversations
            .Select(c => c.VendorId == query.UserId ? c.UserId : c.VendorId)
            .Distinct()
            .ToList();
        var parkingIds = conversations.Select(c => c.ParkingSpaceId).Distinct().ToList();

        // Sequential: all adapters share the same scoped ApplicationDbContext (not thread-safe).
        var unreadByConversation = await _unitOfWork.ChatMessages.GetUnreadCountsByConversationIdsAsync(
            conversationIds, query.UserId, cancellationToken);
        var userNames = (await _userLookup.GetActiveByIdsAsync(otherIds, cancellationToken)).ToDictionary(
            u => u.UserId,
            u => string.IsNullOrWhiteSpace(u.FullName) ? "Unknown" : u.FullName);
        var parkingTitles = (await _parkingSpaceLookup.GetByIdsAsync(parkingIds, cancellationToken)).ToDictionary(
            s => s.ParkingSpaceId,
            s => string.IsNullOrWhiteSpace(s.Title) ? "Unknown" : s.Title);

        var dtos = new List<ConversationDto>(conversations.Count);
        foreach (var conversation in conversations)
        {
            unreadByConversation.TryGetValue(conversation.Id, out var unreadCount);

            var isVendor = conversation.VendorId == query.UserId;
            var otherId = isVendor ? conversation.UserId : conversation.VendorId;
            var otherName = userNames.TryGetValue(otherId, out var n) ? n : "Unknown";
            var parkingTitle = parkingTitles.TryGetValue(conversation.ParkingSpaceId, out var t) ? t : "Unknown";

            dtos.Add(conversation.ToDto(query.UserId, unreadCount, otherName, parkingTitle));
        }

        var result = new ConversationListDto(
            dtos,
            totalCount,
            query.Page,
            query.PageSize,
            (int)Math.Ceiling(totalCount / (double)query.PageSize));

        return new ApiResponse<ConversationListDto>(true, null, result);
    }
}

internal sealed class GetMessagesHandler : IQueryHandler<GetMessagesQuery, ApiResponse<List<ChatMessageDto>>>
{
    private readonly IMessagingUnitOfWork _unitOfWork;
    private readonly IUserLookup _userLookup;

    public GetMessagesHandler(IMessagingUnitOfWork unitOfWork, IUserLookup userLookup)
    {
        _unitOfWork = unitOfWork;
        _userLookup = userLookup;
    }

    public async Task<ApiResponse<List<ChatMessageDto>>> HandleAsync(
        GetMessagesQuery query,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _unitOfWork.Conversations.GetByIdAsync(query.ConversationId, cancellationToken);
        if (conversation == null)
            return new ApiResponse<List<ChatMessageDto>>(false, "Conversation not found", null);

        if (conversation.UserId != query.UserId && conversation.VendorId != query.UserId)
            return new ApiResponse<List<ChatMessageDto>>(false, "Unauthorized", null);

        var messages = (await _unitOfWork.ChatMessages.GetByConversationIdAsync(
            query.ConversationId, query.Page, query.PageSize, cancellationToken)).ToList();

        if (messages.Count == 0)
            return new ApiResponse<List<ChatMessageDto>>(true, null, new List<ChatMessageDto>());

        var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
        var senders = await _userLookup.GetActiveByIdsAsync(senderIds, cancellationToken);
        var senderNames = senders.ToDictionary(
            u => u.UserId,
            u => string.IsNullOrWhiteSpace(u.FullName) ? "Unknown" : u.FullName);

        var dtos = new List<ChatMessageDto>(messages.Count);
        foreach (var message in messages)
        {
            var senderName = senderNames.TryGetValue(message.SenderId, out var n) ? n : "Unknown";
            dtos.Add(message.ToDto(senderName));
        }

        return new ApiResponse<List<ChatMessageDto>>(true, null, dtos);
    }
}

/// <summary>
/// Fast total unread message count (single SQL COUNT — not N+1 conversation load).
/// Short-lived cache for badge polling / visibility resync; invalidated on send/read.
/// </summary>
internal sealed class GetUnreadMessageCountHandler : IQueryHandler<GetUnreadMessageCountQuery, ApiResponse<int>>
{
    private static readonly TimeSpan UnreadCacheTtl = TimeSpan.FromSeconds(20);

    private readonly IMessagingUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public GetUnreadMessageCountHandler(IMessagingUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<int>> HandleAsync(
        GetUnreadMessageCountQuery query,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKeys.ChatUnread(query.UserId);
        var total = await _cache.GetOrSetAsync(
            key,
            () => _unitOfWork.ChatMessages.GetUnreadCountAsync(query.UserId, cancellationToken),
            UnreadCacheTtl,
            cancellationToken);
        return new ApiResponse<int>(true, null, total);
    }
}

internal sealed class CanAccessConversationHandler : IQueryHandler<CanAccessConversationQuery, ApiResponse<bool>>
{
    private readonly IMessagingUnitOfWork _unitOfWork;

    public CanAccessConversationHandler(IMessagingUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<bool>> HandleAsync(
        CanAccessConversationQuery query,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _unitOfWork.Conversations.GetByIdAsync(query.ConversationId, cancellationToken);
        if (conversation == null)
            return new ApiResponse<bool>(false, "Conversation not found", false);

        var allowed = conversation.UserId == query.UserId || conversation.VendorId == query.UserId;
        return allowed
            ? new ApiResponse<bool>(true, null, true)
            : new ApiResponse<bool>(false, "Unauthorized", false);
    }
}
