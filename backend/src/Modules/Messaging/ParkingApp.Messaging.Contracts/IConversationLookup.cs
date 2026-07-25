namespace ParkingApp.Messaging.Contracts;

/// <summary>
/// Messaging module contract: other modules request conversation summaries without repositories.
/// </summary>
public interface IConversationLookup
{
    Task<ConversationSummary?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<ConversationSummary?> GetByParticipantsAsync(
        Guid parkingSpaceId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
