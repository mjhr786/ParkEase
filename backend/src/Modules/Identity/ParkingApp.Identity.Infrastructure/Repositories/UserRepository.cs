using Microsoft.EntityFrameworkCore;
using ParkingApp.BuildingBlocks.ValueObjects;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Identity.Infrastructure.Persistence;

namespace ParkingApp.Identity.Infrastructure.Repositories;

internal class UserRepository : IdentityRepository<User>, IUserRepository
{
    public UserRepository(IIdentityDbContext context) : base((DbContext)context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalized = new Email(email);
            return await _dbSet.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        await _dbSet.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, cancellationToken);
}

internal class VehicleRepository : IdentityRepository<Vehicle>, IVehicleRepository
{
    public VehicleRepository(IIdentityDbContext context) : base((DbContext)context) { }

    public async Task<IEnumerable<Vehicle>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet
            .Where(v => v.UserId == userId && !v.IsDeleted)
            .OrderByDescending(v => v.IsDefault)
            .ThenByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<Vehicle?> GetDefaultVehicleAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.FirstOrDefaultAsync(v => v.UserId == userId && v.IsDefault && !v.IsDeleted, cancellationToken);
}

internal class DeviceTokenRepository : IdentityRepository<DeviceToken>, IDeviceTokenRepository
{
    public DeviceTokenRepository(IIdentityDbContext context) : base((DbContext)context) { }

    public async Task<IEnumerable<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.Where(d => d.UserId == userId).ToListAsync(cancellationToken);

    public async Task<DeviceToken?> GetByDeviceIdAndUserIdAsync(string deviceId, Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.UserId == userId, cancellationToken);

    public async Task<IEnumerable<string>> GetFcmTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.Where(d => d.UserId == userId).Select(d => d.FcmToken).ToListAsync(cancellationToken);
}
