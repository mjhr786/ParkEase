namespace ParkingApp.Identity.Contracts;

/// <summary>
/// Identity contract for FCM/APNs device tokens used by Notifications without Identity Domain.
/// </summary>
public interface IDeviceTokenLookup
{
    Task<IReadOnlyList<string>> GetFcmTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
