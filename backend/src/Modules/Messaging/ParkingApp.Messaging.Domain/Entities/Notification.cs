using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Messaging.Contracts.Enums;

namespace ParkingApp.Messaging.Domain.Entities;

/// <summary>
/// In-app notification record. ID-centric (<see cref="UserId"/>); no Identity navigation.
/// </summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; set; }

    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Data { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }

    public void MarkAsRead(DateTime? readAtUtc = null)
    {
        if (IsRead)
            return;
        IsRead = true;
        ReadAt = readAtUtc ?? DateTime.UtcNow;
    }
}
