using Microsoft.EntityFrameworkCore;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Messaging.Domain.Interfaces;
using ParkingApp.Messaging.Infrastructure.Persistence;

namespace ParkingApp.Messaging.Infrastructure.Repositories;

internal class ConversationRepository : MessagingRepository<Conversation>, IConversationRepository
{
    public ConversationRepository(IMessagingDbContext context) : base((DbContext)context) { }

    public async Task<Conversation?> GetByParticipantsAsync(Guid parkingSpaceId, Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.FirstOrDefaultAsync(c => c.ParkingSpaceId == parkingSpaceId && c.UserId == userId, cancellationToken);

    public async Task<IEnumerable<Conversation>> GetByUserIdAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        await _dbSet
            .Where(c => c.UserId == userId || c.VendorId == userId)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.CountAsync(c => c.UserId == userId || c.VendorId == userId, cancellationToken);
}

internal class ChatMessageRepository : MessagingRepository<ChatMessage>, IChatMessageRepository
{
    public ChatMessageRepository(IMessagingDbContext context) : base((DbContext)context) { }

    public async Task<IEnumerable<ChatMessage>> GetByConversationIdAsync(Guid conversationId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default) =>
        await _dbSet
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        // Join via Conversation navigation (configured FK) — single COUNT, not N+1 per conversation.
        await _dbSet
            .AsNoTracking()
            .Where(m => !m.IsRead && m.SenderId != userId)
            .Where(m => m.Conversation.UserId == userId || m.Conversation.VendorId == userId)
            .CountAsync(cancellationToken);

    public async Task<int> GetUnreadCountByConversationAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.CountAsync(m => m.ConversationId == conversationId && !m.IsRead && m.SenderId != userId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetUnreadCountsByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (conversationIds.Count == 0)
            return new Dictionary<Guid, int>();

        var idList = conversationIds is List<Guid> list
            ? list
            : conversationIds.Distinct().ToList();

        // Single GROUP BY instead of N COUNT queries
        var rows = await _dbSet
            .AsNoTracking()
            .Where(m => idList.Contains(m.ConversationId) && !m.IsRead && m.SenderId != userId)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.ConversationId, r => r.Count);
    }

    public async Task MarkAsReadAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var filter = _dbSet.Where(m =>
            m.ConversationId == conversationId && !m.IsRead && m.SenderId != userId);

        // Production (PostgreSQL): single set-based UPDATE.
        // InMemory (unit tests): ExecuteUpdate is unsupported — fall back to tracked entities.
        if (_context.Database.IsRelational())
        {
            await filter.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(m => m.IsRead, true)
                    .SetProperty(m => m.ReadAt, now)
                    .SetProperty(m => m.UpdatedAt, now),
                cancellationToken);
            return;
        }

        var unreadMessages = await filter.ToListAsync(cancellationToken);
        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
            message.ReadAt = now;
            message.UpdatedAt = now;
        }
    }
}

internal class NotificationRepository : MessagingRepository<Notification>, INotificationRepository
{
    public NotificationRepository(IMessagingDbContext context) : base((DbContext)context) { }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    /// <summary>
    /// Marks all unread notifications for the user as read.
    /// Behavior unchanged: only <see cref="Notification.IsRead"/> and <see cref="Notification.ReadAt"/>;
    /// soft-deleted rows remain excluded by the EF query filter.
    /// </summary>
    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        // Same filter as before (query filter already excludes IsDeleted).
        var filter = _dbSet.Where(n => n.UserId == userId && !n.IsRead);

        // Production (PostgreSQL): single set-based UPDATE — no materialize of large inboxes.
        // InMemory (unit tests): ExecuteUpdate is unsupported — fall back to tracked entities
        // (same outcome after SaveChanges).
        if (_context.Database.IsRelational())
        {
            await filter.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now),
                cancellationToken);
            return;
        }

        var unread = await filter.ToListAsync(cancellationToken);
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }
        _dbSet.UpdateRange(unread);
    }

    /// <summary>
    /// Soft-deletes all notifications for the user (same as <see cref="MessagingRepository{T}.RemoveRange"/>).
    /// Does <b>not</b> hard-delete — preserves historical rows and query-filter visibility rules.
    /// </summary>
    public async Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Same as RemoveRange: IsDeleted = true only (not physical DELETE).
        var filter = _dbSet.Where(n => n.UserId == userId);

        if (_context.Database.IsRelational())
        {
            await filter.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsDeleted, true),
                cancellationToken);
            return;
        }

        var notifications = await filter.ToListAsync(cancellationToken);
        // Soft-delete via base RemoveRange — identical semantics to pre-PR-08 path.
        RemoveRange(notifications);
    }

    public async Task<IReadOnlyList<Notification>> GetPagedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        await _dbSet
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<int> GetTotalCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.CountAsync(n => n.UserId == userId, cancellationToken);
}
