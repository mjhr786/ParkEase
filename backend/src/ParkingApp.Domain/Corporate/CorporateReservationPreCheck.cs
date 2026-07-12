namespace ParkingApp.Domain.Corporate;

/// <summary>
/// Batch of reservation pre-check metrics loaded in a single DB round-trip.
/// Replaces sequential count/overlap queries under the allocation lock.
/// </summary>
public sealed class CorporateReservationPreCheck
{
    public required int DayBookingCount { get; init; }
    public required int WeekBookingCount { get; init; }
    public required int ActiveSharedBookingCount { get; init; }
    public required IReadOnlyList<int> OccupiedSharedSlotNumbers { get; init; }
    public required IReadOnlyDictionary<int, int> SharedSlotUsageBySlot { get; init; }
    public required bool HasOverlappingMemberBooking { get; init; }
    public required bool HasOverlappingVehicleBooking { get; init; }
    public required int RecentBookingCreateCount { get; init; }

    public int AnonymousOccupiedSharedBookings =>
        Math.Max(0, ActiveSharedBookingCount - OccupiedSharedSlotNumbers.Count);
}
