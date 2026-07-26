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

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> SearchForAdminAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(term)
                || u.LastName.ToLower().Contains(term)
                || u.PhoneNumber.ToLower().Contains(term)
                || EF.Property<string>(u, nameof(User.Email)).ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
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
