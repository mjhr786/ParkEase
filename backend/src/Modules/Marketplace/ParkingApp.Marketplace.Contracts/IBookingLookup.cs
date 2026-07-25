namespace ParkingApp.Marketplace.Contracts;

/// <summary>
/// Marketplace module contract: other modules request booking snapshots without repositories.
/// </summary>
public interface IBookingLookup
{
    Task<BookingSnapshot?> GetByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);
}
