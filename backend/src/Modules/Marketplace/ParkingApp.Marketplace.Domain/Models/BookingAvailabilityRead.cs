using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Domain.Models;

/// <summary>
/// Immutable discovery availability row. Search reads these five booking columns and no others.
/// </summary>
public sealed record BookingAvailabilityRead(
    Guid ParkingSpaceId,
    BookingStatus Status,
    DateTime StartDateTime,
    DateTime EndDateTime,
    int? SlotNumber);
