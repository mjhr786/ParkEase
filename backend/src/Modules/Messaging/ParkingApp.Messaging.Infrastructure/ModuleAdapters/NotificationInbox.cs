using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Messaging.Contracts;
using ParkingApp.Messaging.Contracts.Enums;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Messaging.Domain.Interfaces;
using ParkingApp.Messaging.Infrastructure.Persistence;

namespace ParkingApp.Messaging.Infrastructure.ModuleAdapters;

/// <summary>
/// Adapter: contract inbox API over Messaging repositories / DbContext.
/// Uses <see cref="IMessagingDbContext"/> (not UnitOfWork) so notification inserts never re-enter the outbox pipeline.
/// </summary>
internal sealed class NotificationInbox : INotificationInbox
{
    private readonly IMessagingDbContext _db;
    private readonly INotificationRepository _notifications;

    public NotificationInbox(IMessagingDbContext db, INotificationRepository notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task AddAsync(
        Guid userId,
        NotificationType type,
        NotificationPriority priority,
        string title,
        string message,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        // Outbox handlers can retry; avoid duplicate inbox rows for the same booking/payment side-effect.
        if (await IsDuplicateAsync(userId, type, title, message, data, cancellationToken))
            return;

        var entity = new Notification
        {
            UserId = userId,
            Type = type,
            Priority = priority,
            Title = title,
            Message = message,
            Data = data,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Notifications.AddAsync(entity, cancellationToken);
        // ApplicationDbContext.SaveChanges — timestamps only, no outbox re-entry via UnitOfWork.
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationRecord>> GetPagedAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var items = await _notifications.GetPagedAsync(userId, page, pageSize, cancellationToken);
        return items.Select(Map).ToList();
    }

    public Task<int> GetTotalCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _notifications.GetTotalCountAsync(userId, cancellationToken);

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _notifications.GetUnreadCountAsync(userId, cancellationToken);

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await _notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null || notification.UserId != userId)
            return false;

        if (!notification.IsRead)
        {
            notification.MarkAsRead();
            _notifications.Update(notification);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _notifications.MarkAllAsReadAsync(userId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await _notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null || notification.UserId != userId)
            return false;

        _notifications.Remove(notification);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _notifications.DeleteAllAsync(userId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> IsDuplicateAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string? data,
        CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddHours(-24);

        // Prefer stable business keys embedded in Data (BookingId / PaymentId).
        var bookingId = TryGetDataValue(data, "BookingId");
        var paymentId = TryGetDataValue(data, "PaymentId");

        if (!string.IsNullOrWhiteSpace(paymentId) || !string.IsNullOrWhiteSpace(bookingId))
        {
            var recent = await _db.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId
                            && n.Type == type
                            && !n.IsDeleted
                            && n.CreatedAt >= since)
                .Select(n => n.Data)
                .ToListAsync(cancellationToken);

            foreach (var existingData in recent)
            {
                if (!string.IsNullOrWhiteSpace(paymentId)
                    && string.Equals(TryGetDataValue(existingData, "PaymentId"), paymentId, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.IsNullOrWhiteSpace(bookingId)
                    && string.Equals(TryGetDataValue(existingData, "BookingId"), bookingId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(TryGetDataValue(existingData, "Type"), TryGetDataValue(data, "Type"), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        // Fallback: identical title + message for same user/type within a short window (covers non-keyed events).
        return await _db.Notifications
            .AsNoTracking()
            .AnyAsync(n =>
                    n.UserId == userId
                    && n.Type == type
                    && !n.IsDeleted
                    && n.CreatedAt >= since
                    && n.Title == title
                    && n.Message == message,
                cancellationToken);
    }

    private static string? TryGetDataValue(string? dataJson, string key)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            // System.Text.Json dictionary serialization uses the original key casing.
            if (doc.RootElement.TryGetProperty(key, out var exact))
                return exact.GetString();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                    return prop.Value.GetString();
            }
        }
        catch (JsonException)
        {
            // non-JSON data payloads — ignore
        }

        return null;
    }

    private static NotificationRecord Map(Notification n) => new(
        n.Id,
        n.UserId,
        n.Type,
        n.Title,
        n.Message,
        n.Data,
        n.IsRead,
        n.CreatedAt);
}
