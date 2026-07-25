using Microsoft.EntityFrameworkCore;
using ParkingApp.Messaging.Domain.Entities;

namespace ParkingApp.Messaging.Infrastructure.Persistence;

/// <summary>
/// Messaging module persistence facade. Repositories depend on this instead of the full ApplicationDbContext.
/// Implemented by the shared ApplicationDbContext (single database).
/// </summary>
public interface IMessagingDbContext
{
    DbSet<Conversation> Conversations { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<Notification> Notifications { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
