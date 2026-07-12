using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Marketplace;
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
        // Windowed existence checks only — no full user/space booking graphs.
        var hasVehicleOverlap = !string.IsNullOrWhiteSpace(vehicleNumber)
            && await _unitOfWork.Bookings.HasActiveVehicleOverlapAsync(
                userId,
                vehicleNumber!,
                startUtc,
                endUtc,
                excludeBookingId: null,
                cancellationToken);

        var hasOverlap = await _unitOfWork.Bookings.HasOverlappingBookingAsync(
            parking.Id, startUtc, endUtc, null, cancellationToken);

        var activeCount = 0;
        if (hasOverlap)
        {
            activeCount = await _unitOfWork.Bookings.GetActiveBookingsCountAsync(
                parking.Id, startUtc, endUtc, cancellationToken);
        }

        var hasSlotConflict = slotNumber.HasValue
            && await _unitOfWork.Bookings.IsSlotOccupiedInWindowAsync(
                parking.Id,
                slotNumber.Value,
                startUtc,
                endUtc,
                excludeBookingId: null,
                cancellationToken);

        return BookingAvailabilityRules.ValidateCreateFacts(
            parking,
            startUtc,
            endUtc,
            DateTime.UtcNow,
            slotNumber,
            vehicleNumber,
            hasOverlap,
            activeCount,
            hasVehicleOverlap,
            hasSlotConflict);
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
