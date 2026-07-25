using ParkingApp.Messaging.Contracts.Enums;

namespace ParkingApp.Messaging.Contracts;

/// <summary>
/// Cross-module inbox for in-app notifications. Implementations live in Messaging.Infrastructure.
/// No Domain entity types cross the boundary.
/// </summary>
public interface INotificationInbox
{
    Task AddAsync(
        Guid userId,
        NotificationType type,
        NotificationPriority priority,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationRecord>> GetPagedAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetTotalCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>Read model for a persisted in-app notification.</summary>
public sealed record NotificationRecord(
    Guid Id,
    Guid UserId,
    NotificationType Type,
    string Title,
    string Message,
    string? Data,
    bool IsRead,
    DateTime CreatedAt);
