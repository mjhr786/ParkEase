using ParkingApp.Domain.Shared;
namespace ParkingApp.Domain.Identity;

public class DeviceToken : BaseEntity
{
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Unique identifier for the physical device (e.g. Android Installation ID).
    /// Used to upsert — one row per device.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Platform identifier. e.g. "android", "ios"
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Firebase Cloud Messaging token for this device.
    /// </summary>
    public string FcmToken { get; set; } = string.Empty;

    /// <summary>
    /// App version string at time of registration.
    /// </summary>
    public string? AppVersion { get; set; }

    /// <summary>
    /// Updated each time the token is refreshed/re-registered.
    /// </summary>
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual User User { get; set; } = null!;
}
