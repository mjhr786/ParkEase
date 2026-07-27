using ParkingApp.BuildingBlocks.Persistence;
using ParkingApp.Messaging.Domain.Entities;

namespace ParkingApp.Messaging.Domain.Interfaces;

public interface IConversationRepository : IRepository<Conversation>
{
    Task<Conversation?> GetByParticipantsAsync(Guid parkingSpaceId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// When the sender is the vendor and no conversationId was provided: return the single
    /// conversation for this space+vendor if and only if exactly one exists (preserves prior behavior).
    /// Uses Take(2) so popular listings never load the full thread set.
    /// </summary>
    Task<Conversation?> GetSoleByVendorAndSpaceAsync(Guid parkingSpaceId, Guid vendorId, CancellationToken cancellationToken = default);

    Task<IEnumerable<Conversation>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IChatMessageRepository : IRepository<ChatMessage>
{
    Task<IEnumerable<ChatMessage>> GetByConversationIdAsync(Guid conversationId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountByConversationAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Single query: unread counts for the given conversation ids (messages not sent by <paramref name="userId"/>).
    /// Missing ids are omitted from the dictionary (treat as 0).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetUnreadCountsByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks unread messages from the other participant as read.
    /// Returns <c>true</c> when the caller must call <c>SaveChanges</c> (tracked/InMemory path);
    /// <c>false</c> when a set-based UPDATE already persisted (relational).
    /// </summary>
    Task<bool> MarkAsReadAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
}

public interface INotificationRepository : IRepository<Notification>
{
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetPagedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Messaging module unit-of-work port (conversations, chat, in-app notifications).
/// </summary>
public interface IMessagingUnitOfWork : IUnitOfWorkTransaction
{
    IConversationRepository Conversations { get; }
    IChatMessageRepository ChatMessages { get; }
    INotificationRepository Notifications { get; }
}
