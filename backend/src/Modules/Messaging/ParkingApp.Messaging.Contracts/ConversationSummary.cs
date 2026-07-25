namespace ParkingApp.Messaging.Contracts;

/// <summary>
/// Cross-module read model for a chat conversation. No Domain entity types.
/// </summary>
public sealed record ConversationSummary(
    Guid ConversationId,
    Guid ParkingSpaceId,
    Guid UserId,
    Guid VendorId,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    DateTime CreatedAt);
