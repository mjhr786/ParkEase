using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Infrastructure.ModuleAdapters;

/// <summary>
/// Marketplace adapter: maps Booking aggregate to contract snapshot.
/// </summary>
internal sealed class BookingLookup : IBookingLookup
{
    private readonly IBookingRepository _bookings;

    public BookingLookup(IBookingRepository bookings) => _bookings = bookings;

    public async Task<BookingSnapshot?> GetByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookings.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null)
            return null;

        return Map(booking);
    }

    internal static BookingSnapshot Map(Booking booking) =>
        new(
            booking.Id,
            booking.UserId,
            booking.ParkingSpaceId,
            booking.StartDateTime,
            booking.EndDateTime,
            booking.Status.ToString(),
            booking.BookingReference,
            booking.SlotNumber,
            booking.TotalAmount,
            booking.VehicleNumber,
            booking.QRCode);
}
