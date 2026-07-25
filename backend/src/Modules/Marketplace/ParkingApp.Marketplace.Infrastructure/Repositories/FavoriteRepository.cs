using Microsoft.EntityFrameworkCore;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Infrastructure.Persistence;

namespace ParkingApp.Marketplace.Infrastructure.Repositories;

internal sealed class FavoriteRepository : MarketplaceRepository<Favorite>, IFavoriteRepository
{
    public FavoriteRepository(IMarketplaceDbContext context) : base((DbContext)context)
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
        // Include soft-deleted rows so toggle can restore instead of inserting a duplicate
        // (unique index IX_Favorites_UserId_ParkingSpaceId is not filtered by IsDeleted).
        return await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.ParkingSpaceId == parkingSpaceId, cancellationToken);
    }
}
