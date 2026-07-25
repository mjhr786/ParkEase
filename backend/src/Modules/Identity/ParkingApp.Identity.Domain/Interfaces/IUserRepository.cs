using ParkingApp.BuildingBlocks.Persistence;
using ParkingApp.Identity.Domain.Entities;

namespace ParkingApp.Identity.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public interface IVehicleRepository : IRepository<Vehicle>
{
    Task<IEnumerable<Vehicle>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetDefaultVehicleAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IDeviceTokenRepository : IRepository<DeviceToken>
{
    Task<IEnumerable<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<DeviceToken?> GetByDeviceIdAndUserIdAsync(string deviceId, Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetFcmTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Identity module unit-of-work port (users, vehicles, device tokens).
/// </summary>
public interface IIdentityUnitOfWork : IUnitOfWorkTransaction
{
    IUserRepository Users { get; }
    IVehicleRepository Vehicles { get; }
    IDeviceTokenRepository DeviceTokens { get; }
}
