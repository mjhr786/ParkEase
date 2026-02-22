namespace ParkingApp.Application.DTOs;

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
    DateTime CreatedAt
);

public record SendMessageDto(
    Guid ParkingSpaceId,
    string Content
);
