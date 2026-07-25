using ParkingApp.Application.DTOs;
using ParkingApp.Messaging.Application.DTOs;


using ParkingApp.Messaging.Domain.Entities;

namespace ParkingApp.Messaging.Application.Mappings;

/// <summary>
/// Messaging module mappings. Display names for cross-module parties are supplied by the caller
/// (via Identity/Marketplace contracts) G�� entities are ID-centric.
/// </summary>
public static class MessagingMappings
{
    public static ChatMessageDto ToDto(
        this ChatMessage message,
        string? senderName = null,
        Guid? recipientId = null) => new(
        message.Id,
        message.ConversationId,
        message.SenderId,
        string.IsNullOrWhiteSpace(senderName) ? "Unknown" : senderName,
        message.Content,
        message.IsRead,
        message.CreatedAt,
        recipientId
    );

    public static ConversationDto ToDto(
        this Conversation conversation,
        Guid currentUserId,
        int unreadCount = 0,
        string? otherParticipantName = null,
        string? parkingSpaceTitle = null)
    {
        var isVendor = conversation.VendorId == currentUserId;
        var otherParticipantId = isVendor ? conversation.UserId : conversation.VendorId;
        return new ConversationDto(
            conversation.Id,
            conversation.ParkingSpaceId,
            string.IsNullOrWhiteSpace(parkingSpaceTitle) ? "Unknown" : parkingSpaceTitle,
            otherParticipantId,
            string.IsNullOrWhiteSpace(otherParticipantName) ? "Unknown" : otherParticipantName,
            conversation.LastMessagePreview,
            conversation.LastMessageAt,
            unreadCount,
            conversation.CreatedAt
        );
    }
}
