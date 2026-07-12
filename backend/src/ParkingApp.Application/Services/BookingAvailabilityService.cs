using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Services;

namespace ParkingApp.Application.Services;

public sealed class BookingAvailabilityService : IBookingAvailabilityService
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public BookingAvailabilityService(IMarketplaceUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<BookingAvailabilityResult> CanCreateAsync(
        Guid userId,
        ParkingSpace parking,
        DateTime startUtc,
        DateTime endUtc,
        int? slotNumber,
        string? vehicleNumber,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Booking> userBookings = Array.Empty<Booking>();
        if (!string.IsNullOrWhiteSpace(vehicleNumber))
        {
            var loaded = await _unitOfWork.Bookings.GetByUserIdAsync(userId, cancellationToken);
            userBookings = loaded?.ToList() ?? new List<Booking>();
        }

        var hasOverlap = await _unitOfWork.Bookings.HasOverlappingBookingAsync(
            parking.Id, startUtc, endUtc, null, cancellationToken);

        var activeCount = 0;
        if (hasOverlap)
        {
            activeCount = await _unitOfWork.Bookings.GetActiveBookingsCountAsync(
                parking.Id, startUtc, endUtc, cancellationToken);
        }

        IReadOnlyCollection<Booking> slotCandidates = Array.Empty<Booking>();
        if (slotNumber.HasValue)
        {
            var spaceBookings = await _unitOfWork.Bookings.GetByParkingSpaceIdAsync(parking.Id, cancellationToken);
            slotCandidates = (spaceBookings ?? Enumerable.Empty<Booking>())
                .Where(b =>
                    BookingAvailabilityRules.IsActiveBookingStatus(b.Status) &&
                    BookingAvailabilityRules.TimeRangesOverlap(b.StartDateTime, b.EndDateTime, startUtc, endUtc))
                .ToList();
        }

        return BookingAvailabilityRules.ValidateCreateFacts(
            parking,
            startUtc,
            endUtc,
            DateTime.UtcNow,
            slotNumber,
            vehicleNumber,
            userBookings,
            hasOverlap,
            activeCount,
            slotCandidates);
    }

    public async Task<BookingAvailabilityResult> CanRescheduleAsync(
        Booking booking,
        ParkingSpace parking,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        var hasOverlap = await _unitOfWork.Bookings.HasOverlappingBookingAsync(
            parking.Id, startUtc, endUtc, booking.Id, cancellationToken);

        var activeCount = 0;
        if (hasOverlap)
        {
            activeCount = await _unitOfWork.Bookings.GetActiveBookingsCountAsync(
                parking.Id, startUtc, endUtc, cancellationToken);
        }

        return BookingAvailabilityRules.ValidateRescheduleFacts(
            parking,
            startUtc,
            endUtc,
            DateTime.UtcNow,
            hasOverlap,
            activeCount);
    }

    public async Task<BookingAvailabilityResult> CanExtendAsync(
        Booking booking,
        ParkingSpace parking,
        DateTime extensionStartUtc,
        DateTime extensionEndUtc,
        CancellationToken cancellationToken = default)
    {
        if (extensionEndUtc <= extensionStartUtc)
            return BookingAvailabilityResult.Fail("Extension end date/time must be greater than current booking end date/time");

        var hasOverlap = await _unitOfWork.Bookings.HasOverlappingBookingAsync(
            parking.Id, extensionStartUtc, extensionEndUtc, booking.Id, cancellationToken);

        if (!hasOverlap)
            return BookingAvailabilityResult.Ok();

        var activeCount = await _unitOfWork.Bookings.GetActiveBookingsCountAsync(
            parking.Id, extensionStartUtc, extensionEndUtc, cancellationToken);

        if (activeCount >= parking.TotalSpots)
            return BookingAvailabilityResult.Fail("Parking spot is not available for the extended period");

        return BookingAvailabilityResult.Ok();
    }
}
