using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Data { get; set; } // JSON serialized extra data, if any
    
    // Read Status tracking
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
    
    // Navigation Property
    public virtual User User { get; set; } = null!;
}
