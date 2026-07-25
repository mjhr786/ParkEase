using ParkingApp.BuildingBlocks.Domain;

namespace ParkingApp.Identity.Domain.Entities;

public class DeviceToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Unique identifier for the physical device (e.g. Android Installation ID).</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Platform identifier. e.g. "android", "ios"</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Firebase Cloud Messaging token for this device.</summary>
    public string FcmToken { get; set; } = string.Empty;

    public string? AppVersion { get; set; }

    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; } = null!;
}
