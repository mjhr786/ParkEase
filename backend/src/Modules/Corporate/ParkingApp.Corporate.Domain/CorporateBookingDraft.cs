using ParkingApp.Domain.Enums;
using BookingStatus = ParkingApp.Marketplace.Contracts.Enums.BookingStatus;
using VehicleType = ParkingApp.BuildingBlocks.Enums.VehicleType;

namespace ParkingApp.Corporate.Domain;

/// <summary>
/// Marketplace booking facts required by Corporate reservation rules.
/// Keeps Corporate domain methods free of the Marketplace <c>Booking</c> aggregate type.
/// Application maps Booking ΓåÆ draft before calling Reserve*.
/// </summary>
public sealed class CorporateBookingDraft
{
    public Guid BookingId { get; }
    public Guid ParkingSpaceId { get; }
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }
    public BookingStatus Status { get; }
    public VehicleType VehicleType { get; }
    public string? VehicleNumber { get; }

    public double DurationHours => (EndUtc - StartUtc).TotalHours;

    public CorporateBookingDraft(
        Guid bookingId,
        Guid parkingSpaceId,
        DateTime startUtc,
        DateTime endUtc,
        BookingStatus status,
        VehicleType vehicleType,
        string? vehicleNumber)
    {
        if (bookingId == Guid.Empty)
            throw new ArgumentException("Booking ID is required.", nameof(bookingId));
        if (parkingSpaceId == Guid.Empty)
            throw new ArgumentException("Parking space ID is required.", nameof(parkingSpaceId));
        if (endUtc <= startUtc)
            throw new ArgumentException("Booking end time must be after the start time.", nameof(endUtc));

        BookingId = bookingId;
        ParkingSpaceId = parkingSpaceId;
        StartUtc = startUtc.Kind == DateTimeKind.Utc ? startUtc : DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        EndUtc = endUtc.Kind == DateTimeKind.Utc ? endUtc : DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
        Status = status;
        VehicleType = vehicleType;
        VehicleNumber = string.IsNullOrWhiteSpace(vehicleNumber)
            ? null
            : vehicleNumber.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Marketplace mutations the application layer must apply after a successful reservation.
    /// </summary>
    public MarketplaceBookingAdjustment ToConfirmationAdjustment(int? slotNumber) =>
        new(
            ShouldConfirm: Status is BookingStatus.Pending or BookingStatus.AwaitingPayment,
            RequiresConfirmedStatus: Status is not (BookingStatus.Pending or BookingStatus.AwaitingPayment or BookingStatus.Confirmed),
            SlotNumber: slotNumber);
}

/// <summary>
/// Instructions for Application to apply against the Marketplace booking aggregate
/// after Corporate reservation succeeds (same-process sync command).
/// </summary>
public readonly record struct MarketplaceBookingAdjustment(
    bool ShouldConfirm,
    bool RequiresConfirmedStatus,
    int? SlotNumber);

