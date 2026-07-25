namespace ParkingApp.Messaging.Application.DTOs;

public record ConversationDto(
    Guid Id,
    Guid ParkingSpaceId,
    string ParkingSpaceTitle,
    Guid OtherParticipantId,
    string OtherParticipantName,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    int UnreadCount,
    DateTime CreatedAt
);

public record ConversationListDto(
    List<ConversationDto> Conversations,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string Content,
    bool IsRead,
    DateTime CreatedAt,
    /// <summary>
    /// Other participant (for SignalR fan-out). Null on historical list payloads.
    /// </summary>
    Guid? RecipientId = null
);

/// <summary>
/// Result of marking a conversation read. <see cref="OtherParticipantId"/> drives SignalR receipt without reloading the inbox.
/// </summary>
public record MarkMessagesReadResult(
    bool Marked,
    Guid OtherParticipantId
);

public record SendMessageDto(
    Guid ParkingSpaceId,
    string Content,
    Guid? ConversationId = null
);
