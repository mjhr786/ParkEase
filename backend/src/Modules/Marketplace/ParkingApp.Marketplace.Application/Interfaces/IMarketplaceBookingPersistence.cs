using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Application.Interfaces;

/// <summary>
/// Transitional Marketplace write facade for Corporate flows that still orchestrate
/// the domain <see cref="Booking"/> aggregate (ReserveEmployeeParking / ReserveVisitorParking).
/// Prefer <see cref="Contracts.Marketplace.IMarketplaceBookingService"/> for ID/snapshot-based APIs.
/// Does not call SaveChanges GÇö shared UnitOfWork ownership stays with the caller.
/// </summary>
public interface IMarketplaceBookingPersistence
{
    Task StageNewAsync(Booking booking, CancellationToken cancellationToken = default);
    void Update(Booking booking);
}


