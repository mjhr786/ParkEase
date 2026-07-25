namespace ParkingApp.Messaging.Contracts;

/// <summary>
/// Cross-module read model for a chat message. No Domain entity types.
/// </summary>
public sealed record ChatMessageSummary(
    Guid MessageId,
    Guid ConversationId,
    Guid SenderId,
    string Content,
    bool IsRead,
    DateTime CreatedAt);
