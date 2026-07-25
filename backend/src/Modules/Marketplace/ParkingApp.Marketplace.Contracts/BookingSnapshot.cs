namespace ParkingApp.Marketplace.Contracts;

/// <summary>
/// Cross-module marketplace booking read model. Status is a string to avoid Domain enum coupling.
/// Extended fields support Corporate DTO mapping without loading the Booking aggregate.
/// </summary>
public sealed record BookingSnapshot(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    DateTime StartUtc,
    DateTime EndUtc,
    string Status,
    string? BookingReference = null,
    int? SlotNumber = null,
    decimal TotalAmount = 0m,
    string? VehicleNumber = null,
    string? QrCode = null);
