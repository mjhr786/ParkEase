using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Services;

/// <summary>
/// Result of a marketplace booking availability / conflict check.
/// </summary>
public sealed class BookingAvailabilityResult
{
    public bool IsAllowed { get; }
    public string? ErrorMessage { get; }

    private BookingAvailabilityResult(bool isAllowed, string? errorMessage)
    {
        IsAllowed = isAllowed;
        ErrorMessage = errorMessage;
    }

    public static BookingAvailabilityResult Ok() => new(true, null);
    public static BookingAvailabilityResult Fail(string message) => new(false, message);
}

/// <summary>
/// Pure domain rules for marketplace booking windows, vehicle conflicts,
/// capacity, and slot assignment. No I/O — callers load facts and pass them in.
/// </summary>
public static class BookingAvailabilityRules
{
    private static readonly BookingStatus[] ActiveStatuses =
    {
        BookingStatus.Pending,
        BookingStatus.AwaitingPayment,
        BookingStatus.Confirmed,
        BookingStatus.InProgress
    };

    public static bool IsActiveBookingStatus(BookingStatus status) =>
        status is BookingStatus.Pending
            or BookingStatus.AwaitingPayment
            or BookingStatus.Confirmed
            or BookingStatus.InProgress;

    public static bool TimeRangesOverlap(DateTime startA, DateTime endA, DateTime startB, DateTime endB) =>
        startA < endB && endA > startB;

    public static BookingAvailabilityResult ValidateTimeWindow(
        DateTime startUtc,
        DateTime endUtc,
        DateTime utcNow,
        bool requireStartInFuture = true)
    {
        if (endUtc <= startUtc)
            return BookingAvailabilityResult.Fail("End date must be after start date");

        if (requireStartInFuture && startUtc < utcNow)
            return BookingAvailabilityResult.Fail("Start date must be in the future");

        return BookingAvailabilityResult.Ok();
    }

    public static BookingAvailabilityResult ValidateSlotNumber(int? slotNumber, int totalSpots)
    {
        if (!slotNumber.HasValue)
            return BookingAvailabilityResult.Ok();

        if (slotNumber.Value < 1 || slotNumber.Value > totalSpots)
            return BookingAvailabilityResult.Fail($"Slot must be between 1 and {totalSpots}");

        return BookingAvailabilityResult.Ok();
    }

    /// <summary>
    /// Vehicle cannot have another active booking for an overlapping interval.
    /// </summary>
    public static BookingAvailabilityResult ValidateVehicleOverlap(
        string? vehicleNumber,
        IEnumerable<Booking> candidateUserBookings,
        DateTime startUtc,
        DateTime endUtc,
        Guid? excludeBookingId = null)
    {
        if (string.IsNullOrWhiteSpace(vehicleNumber))
            return BookingAvailabilityResult.Ok();

        var normalized = vehicleNumber.Trim();
        var overlap = candidateUserBookings.FirstOrDefault(b =>
            (!excludeBookingId.HasValue || b.Id != excludeBookingId.Value) &&
            !string.IsNullOrWhiteSpace(b.VehicleNumber) &&
            b.VehicleNumber.Trim().Equals(normalized, StringComparison.OrdinalIgnoreCase) &&
            IsActiveBookingStatus(b.Status) &&
            TimeRangesOverlap(b.StartDateTime, b.EndDateTime, startUtc, endUtc));

        if (overlap != null)
        {
            return BookingAvailabilityResult.Fail(
                $"Vehicle {vehicleNumber} is already booked during this time (Ref: {overlap.BookingReference})");
        }

        return BookingAvailabilityResult.Ok();
    }

    /// <summary>
    /// When the space already has overlapping bookings, reject if capacity is full.
    /// </summary>
    public static BookingAvailabilityResult ValidateCapacity(
        bool hasOverlappingBookings,
        int activeOverlappingCount,
        int totalSpots)
    {
        if (!hasOverlappingBookings)
            return BookingAvailabilityResult.Ok();

        if (activeOverlappingCount >= totalSpots)
            return BookingAvailabilityResult.Fail("No spots available for the selected time");

        return BookingAvailabilityResult.Ok();
    }

    /// <summary>
    /// Selected slot must not already be held by an active overlapping booking.
    /// </summary>
    public static BookingAvailabilityResult ValidateSlotConflict(
        int? slotNumber,
        IEnumerable<Booking> spaceBookingsInWindow,
        Guid? excludeBookingId = null)
    {
        if (!slotNumber.HasValue)
            return BookingAvailabilityResult.Ok();

        var conflict = spaceBookingsInWindow.Any(b =>
            (!excludeBookingId.HasValue || b.Id != excludeBookingId.Value) &&
            b.SlotNumber == slotNumber.Value &&
            IsActiveBookingStatus(b.Status));

        if (conflict)
            return BookingAvailabilityResult.Fail(
                $"Slot {slotNumber.Value} is already booked for the selected time");

        return BookingAvailabilityResult.Ok();
    }

    /// <summary>
    /// Run create-path rules that do not require repository I/O beyond facts already loaded.
    /// Capacity/slot still need pre-fetched overlap count / booking list.
    /// </summary>
    public static BookingAvailabilityResult ValidateCreateFacts(
        ParkingSpace parking,
        DateTime startUtc,
        DateTime endUtc,
        DateTime utcNow,
        int? slotNumber,
        string? vehicleNumber,
        IReadOnlyCollection<Booking> userBookings,
        bool hasSpaceOverlap,
        int activeSpaceBookingCount,
        IReadOnlyCollection<Booking> spaceBookingsForSlotCheck)
    {
        if (!parking.IsActive)
            return BookingAvailabilityResult.Fail("Parking space is not available");

        var window = ValidateTimeWindow(startUtc, endUtc, utcNow);
        if (!window.IsAllowed) return window;

        var slotRange = ValidateSlotNumber(slotNumber, parking.TotalSpots);
        if (!slotRange.IsAllowed) return slotRange;

        var vehicle = ValidateVehicleOverlap(vehicleNumber, userBookings, startUtc, endUtc);
        if (!vehicle.IsAllowed) return vehicle;

        var capacity = ValidateCapacity(hasSpaceOverlap, activeSpaceBookingCount, parking.TotalSpots);
        if (!capacity.IsAllowed) return capacity;

        return ValidateSlotConflict(slotNumber, spaceBookingsForSlotCheck);
    }

    public static BookingAvailabilityResult ValidateRescheduleFacts(
        ParkingSpace parking,
        DateTime startUtc,
        DateTime endUtc,
        DateTime utcNow,
        bool hasSpaceOverlap,
        int activeSpaceBookingCount)
    {
        if (!parking.IsActive)
            return BookingAvailabilityResult.Fail("Parking space is not available");

        var window = ValidateTimeWindow(startUtc, endUtc, utcNow);
        if (!window.IsAllowed) return window;

        // Update path used a slightly different capacity message historically
        if (hasSpaceOverlap && activeSpaceBookingCount >= parking.TotalSpots)
            return BookingAvailabilityResult.Fail("No spots available for new dates");

        return BookingAvailabilityResult.Ok();
    }
}
