using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Repositories;

public class FavoriteRepository : Repository<Favorite>, IFavoriteRepository
{
    public FavoriteRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Favorite>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(f => f.ParkingSpace)
            .Where(f => f.UserId == userId && !f.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<Favorite?> GetByUserAndSpaceAsync(Guid userId, Guid parkingSpaceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ParkingSpaceId == parkingSpaceId && !f.IsDeleted, cancellationToken);
    }
}
