using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Services;

/// <summary>Shared bay auto-assignment when facility has indoor guidance enabled.</summary>
internal static class BayAssignmentHelper
{
    private static readonly BookingStatus[] BlockingStatuses =
    {
        BookingStatus.Pending,
        BookingStatus.Confirmed,
        BookingStatus.InProgress,
        BookingStatus.AwaitingPayment,
        BookingStatus.PendingExtension,
        BookingStatus.AwaitingExtensionPayment
    };

    public static async Task TryApplyAsync(
        IMarketplaceUnitOfWork unitOfWork,
        Booking booking,
        CancellationToken cancellationToken = default)
    {
        var space = booking.ParkingSpace
            ?? await unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
        if (space is null || !space.IsBayGuidanceEnabled)
            return;

        var peers = await unitOfWork.Bookings.GetByParkingSpaceIdAsync(space.Id, cancellationToken);
        TryApplyOnConfirm(booking, space, peers);
    }

    public static void TryApplyOnConfirm(Booking booking, ParkingSpace space, IEnumerable<Booking>? overlapping = null)
    {
        if (space is null || !space.IsBayGuidanceEnabled)
            return;

        int? freeSlot = null;
        if (!booking.SlotNumber.HasValue)
            freeSlot = FindFreeSlot(space, booking, overlapping);

        booking.TryAutoAssignBayFromFacility(
            space.IsBayGuidanceEnabled,
            space.DefaultFacilityLevel,
            space.DefaultFacilityZone,
            freeSlot);
    }

    public static int? FindFreeSlot(ParkingSpace space, Booking target, IEnumerable<Booking>? candidates)
    {
        if (space.TotalSpots < 1)
            return 1;

        var taken = new HashSet<int>();
        if (candidates != null)
        {
            foreach (var b in candidates)
            {
                if (b.Id == target.Id)
                    continue;
                if (!b.SlotNumber.HasValue)
                    continue;
                if (!BlockingStatuses.Contains(b.Status))
                    continue;
                // Overlap if windows intersect
                if (b.StartDateTime < target.EndDateTime && b.EndDateTime > target.StartDateTime)
                    taken.Add(b.SlotNumber.Value);
            }
        }

        for (var i = 1; i <= space.TotalSpots; i++)
        {
            if (!taken.Contains(i))
                return i;
        }

        // Overbooked / all slots labeled — still assign next logical number for guidance only
        return space.TotalSpots;
    }
}
