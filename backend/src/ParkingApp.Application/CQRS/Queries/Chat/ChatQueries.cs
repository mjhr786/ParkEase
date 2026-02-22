using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Queries.Chat;

// ────────────────────────────────────────────────────────────────
// Queries
// ────────────────────────────────────────────────────────────────

public sealed record GetConversationsQuery(Guid UserId, int Page = 1, int PageSize = 20) : IQuery<ApiResponse<ConversationListDto>>;
public sealed record GetMessagesQuery(Guid UserId, Guid ConversationId, int Page = 1, int PageSize = 50) : IQuery<ApiResponse<List<ChatMessageDto>>>;

// ────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────

public sealed class GetConversationsHandler : IQueryHandler<GetConversationsQuery, ApiResponse<ConversationListDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetConversationsHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<ConversationListDto>> HandleAsync(GetConversationsQuery query, CancellationToken cancellationToken = default)
    {
        var conversations = await _unitOfWork.Conversations.GetByUserIdAsync(
            query.UserId, query.Page, query.PageSize, cancellationToken);
        var totalCount = await _unitOfWork.Conversations.CountByUserIdAsync(query.UserId, cancellationToken);

        var dtos = new List<ConversationDto>();
        foreach (var conversation in conversations)
        {
            var unreadCount = await _unitOfWork.ChatMessages.GetUnreadCountByConversationAsync(
                conversation.Id, query.UserId, cancellationToken);
            dtos.Add(conversation.ToDto(query.UserId, unreadCount));
        }

        var result = new ConversationListDto(
            dtos,
            totalCount,
            query.Page,
            query.PageSize,
            (int)Math.Ceiling(totalCount / (double)query.PageSize)
        );

        return new ApiResponse<ConversationListDto>(true, null, result);
    }
}

public sealed class GetMessagesHandler : IQueryHandler<GetMessagesQuery, ApiResponse<List<ChatMessageDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMessagesHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<List<ChatMessageDto>>> HandleAsync(GetMessagesQuery query, CancellationToken cancellationToken = default)
    {
        // Verify user is a participant of this conversation
        var conversation = await _unitOfWork.Conversations.GetByIdAsync(query.ConversationId, cancellationToken);
        if (conversation == null)
            return new ApiResponse<List<ChatMessageDto>>(false, "Conversation not found", null);

        if (conversation.UserId != query.UserId && conversation.VendorId != query.UserId)
            return new ApiResponse<List<ChatMessageDto>>(false, "Unauthorized", null);

        var messages = await _unitOfWork.ChatMessages.GetByConversationIdAsync(
            query.ConversationId, query.Page, query.PageSize, cancellationToken);

        var dtos = messages.Select(m => m.ToDto()).ToList();
        return new ApiResponse<List<ChatMessageDto>>(true, null, dtos);
    }
}
