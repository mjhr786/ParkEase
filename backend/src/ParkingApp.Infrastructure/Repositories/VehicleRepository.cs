using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Repositories;

public class VehicleRepository : Repository<Vehicle>, IVehicleRepository
{
    public VehicleRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Vehicle>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(v => v.UserId == userId && !v.IsDeleted)
            .OrderByDescending(v => v.IsDefault)
            .ThenByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Vehicle?> GetDefaultVehicleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(v => v.UserId == userId && v.IsDefault && !v.IsDeleted, cancellationToken);
    }
}
