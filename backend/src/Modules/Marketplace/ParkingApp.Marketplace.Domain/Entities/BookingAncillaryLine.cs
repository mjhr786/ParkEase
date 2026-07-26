using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;

namespace ParkingApp.Marketplace.Domain.Entities;

/// <summary>
/// Snapshot of an ancillary service selected on a booking (price/name locked at create).
/// </summary>
public class BookingAncillaryLine : BaseEntity
{
    public Guid BookingId { get; internal set; }

    /// <summary>Catalog service id when known; null if catalog row was removed later.</summary>
    public Guid? ServiceId { get; internal set; }

    public string SnapshotName { get; internal set; } = string.Empty;
    public decimal UnitPrice { get; internal set; }
    public int Quantity { get; internal set; } = 1;

    public virtual Booking? Booking { get; internal set; }

    public decimal LineTotal => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);

    internal BookingAncillaryLine()
    {
    }

    public static BookingAncillaryLine Create(
        Guid bookingId,
        string snapshotName,
        decimal unitPrice,
        int quantity = 1,
        Guid? serviceId = null)
    {
        if (bookingId == Guid.Empty)
            throw new ValidationException("bookingId", "Booking is required");
        if (string.IsNullOrWhiteSpace(snapshotName))
            throw new ValidationException("snapshotName", "Service name is required");
        if (unitPrice < 0)
            throw new ValidationException("unitPrice", "Unit price cannot be negative");
        if (quantity < 1)
            throw new ValidationException("quantity", "Quantity must be at least 1");

        return new BookingAncillaryLine
        {
            BookingId = bookingId,
            ServiceId = serviceId is { } sid && sid != Guid.Empty ? sid : null,
            SnapshotName = snapshotName.Trim(),
            UnitPrice = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero),
            Quantity = quantity
        };
    }
}
