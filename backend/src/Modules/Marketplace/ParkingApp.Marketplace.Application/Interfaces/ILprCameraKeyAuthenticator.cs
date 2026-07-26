namespace ParkingApp.Marketplace.Application.Interfaces;

/// <summary>
/// Resolves an IoT X-Api-Key against facility-scoped DB keys and config fallback keys.
/// </summary>
public interface ILprCameraKeyAuthenticator
{
    Task<LprApiKeyAuthResult?> AuthenticateAsync(string secret, CancellationToken cancellationToken = default);
}

/// <param name="AllowedParkingSpaceIds">Empty = unrestricted (config-only keys). Non-empty = facility-scoped.</param>
public sealed record LprApiKeyAuthResult(
    string KeyId,
    IReadOnlyList<Guid> AllowedParkingSpaceIds,
    bool FromDatabase
);
