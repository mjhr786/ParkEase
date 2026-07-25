using ParkingApp.Identity.Contracts;
using ParkingApp.Identity.Domain.Interfaces;

namespace ParkingApp.Identity.Infrastructure.ModuleAdapters;

internal sealed class DeviceTokenLookup : IDeviceTokenLookup
{
    private readonly IDeviceTokenRepository _tokens;

    public DeviceTokenLookup(IDeviceTokenRepository tokens) => _tokens = tokens;

    public async Task<IReadOnlyList<string>> GetFcmTokensByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _tokens.GetFcmTokensByUserIdAsync(userId, cancellationToken);
        return tokens.ToList();
    }
}
