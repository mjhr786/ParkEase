using ParkingApp.Messaging.Contracts;
using ParkingApp.Messaging.Domain.Interfaces;

namespace ParkingApp.Messaging.Infrastructure.ModuleAdapters;

/// <summary>
/// Messaging adapter: maps Conversation aggregate to contract summary.
/// </summary>
internal sealed class ConversationLookup : IConversationLookup
{
    private readonly IConversationRepository _conversations;

    public ConversationLookup(IConversationRepository conversations) => _conversations = conversations;

    public async Task<ConversationSummary?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await _conversations.GetByIdAsync(conversationId, cancellationToken);
        return conversation is null ? null : ToSummary(conversation);
    }

    public async Task<ConversationSummary?> GetByParticipantsAsync(
        Guid parkingSpaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversations.GetByParticipantsAsync(parkingSpaceId, userId, cancellationToken);
        return conversation is null ? null : ToSummary(conversation);
    }

    private static ConversationSummary ToSummary(Messaging.Domain.Entities.Conversation conversation) => new(
        conversation.Id,
        conversation.ParkingSpaceId,
        conversation.UserId,
        conversation.VendorId,
        conversation.LastMessagePreview,
        conversation.LastMessageAt,
        conversation.CreatedAt);
}
