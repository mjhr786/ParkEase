using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Repositories;

public class DeviceTokenRepository : Repository<DeviceToken>, IDeviceTokenRepository
{
    public DeviceTokenRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeviceToken?> GetByDeviceIdAndUserIdAsync(string deviceId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.UserId == userId, cancellationToken);
    }

    public async Task<IEnumerable<string>> GetFcmTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.UserId == userId)
            .Select(d => d.FcmToken)
            .ToListAsync(cancellationToken);
    }
}
