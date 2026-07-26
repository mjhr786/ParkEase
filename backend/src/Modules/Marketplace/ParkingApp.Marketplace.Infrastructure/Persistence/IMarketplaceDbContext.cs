using Microsoft.EntityFrameworkCore;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Infrastructure.Persistence;

/// <summary>
/// Marketplace module persistence facade over the shared ApplicationDbContext.
/// </summary>
public interface IMarketplaceDbContext
{
    DbSet<ParkingSpace> ParkingSpaces { get; }
    DbSet<ParkingAvailability> ParkingAvailabilities { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<ParkingPass> ParkingPasses { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<LprAccessAttempt> LprAccessAttempts { get; }
    DbSet<LprCameraKey> LprCameraKeys { get; }
    DbSet<LprPlateRule> LprPlateRules { get; }
    DbSet<EventParkingPackage> EventParkingPackages { get; }
    DbSet<EvChargingSession> EvChargingSessions { get; }
    DbSet<ParkingAncillaryService> ParkingAncillaryServices { get; }
    DbSet<BookingAncillaryLine> BookingAncillaryLines { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
