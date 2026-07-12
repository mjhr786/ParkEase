using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Services;

namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Application-facing booking availability checks (loads repo facts, applies domain rules).
/// </summary>
public interface IBookingAvailabilityService
{
    /// <summary>
    /// Rules for creating a new marketplace booking on an active parking space.
    /// </summary>
    Task<BookingAvailabilityResult> CanCreateAsync(
        Guid userId,
        ParkingSpace parking,
        DateTime startUtc,
        DateTime endUtc,
        int? slotNumber,
        string? vehicleNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rules for rescheduling an existing marketplace booking.
    /// </summary>
    Task<BookingAvailabilityResult> CanRescheduleAsync(
        Booking booking,
        ParkingSpace parking,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Capacity check for extending a booking (current end → new end).
    /// </summary>
    Task<BookingAvailabilityResult> CanExtendAsync(
        Booking booking,
        ParkingSpace parking,
        DateTime extensionStartUtc,
        DateTime extensionEndUtc,
        CancellationToken cancellationToken = default);
}
