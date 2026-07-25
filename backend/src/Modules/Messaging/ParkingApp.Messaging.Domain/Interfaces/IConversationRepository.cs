using ParkingApp.BuildingBlocks.Persistence;
using ParkingApp.Messaging.Domain.Entities;

namespace ParkingApp.Messaging.Domain.Interfaces;

public interface IConversationRepository : IRepository<Conversation>
{
    Task<Conversation?> GetByParticipantsAsync(Guid parkingSpaceId, Guid userId, CancellationToken cancellationToken = default);
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

    Task MarkAsReadAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
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
