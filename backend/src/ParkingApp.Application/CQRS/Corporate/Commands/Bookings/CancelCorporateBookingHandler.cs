using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Bookings;

public class CancelCorporateBookingHandler : ICommandHandler<CancelCorporateBookingCommand, ApiResponse<CorporateBookingDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public CancelCorporateBookingHandler(
        ICorporateUnitOfWork corporate,
        IMarketplaceUnitOfWork marketplace,
        ICacheService cache,
        ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _marketplace = marketplace;
        _cache = cache;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<CorporateBookingDto>> HandleAsync(
        CancelCorporateBookingCommand command,
        CancellationToken ct = default)
    {
        var membership = await _corporate.Companies.GetMembershipAsync(command.CompanyId, command.UserId, ct);
        if (membership == null || !membership.IsActive)
        {
            return new ApiResponse<CorporateBookingDto>(false, "Access denied. You are not an active member of this company.", null);
        }

        var corporateBooking = await _corporate.CorporateBookings.GetByCompanyAndBookingIdAsync(
            command.CompanyId,
            command.BookingId,
            ct);
        if (corporateBooking == null)
        {
            return new ApiResponse<CorporateBookingDto>(false, "Corporate booking not found.", null);
        }

        if (!membership.IsAdmin && corporateBooking.MembershipId != membership.Id)
        {
            return new ApiResponse<CorporateBookingDto>(false, "You can only cancel your own corporate bookings.", null);
        }

        var booking = await _marketplace.Bookings.GetByIdWithDetailsAsync(command.BookingId, ct);
        if (booking == null || booking.IsDeleted)
        {
            return new ApiResponse<CorporateBookingDto>(false, "Booking not found.", null);
        }

        // Keep a clear rule for UI: only active/upcoming reservations.
        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled
            or BookingStatus.Expired or BookingStatus.Rejected)
        {
            return new ApiResponse<CorporateBookingDto>(
                false,
                $"Cannot cancel a booking in {booking.Status} status.",
                null);
        }

        try
        {
            var reason = string.IsNullOrWhiteSpace(command.Reason)
                ? (membership.IsAdmin ? "Cancelled by company admin" : "Cancelled by employee")
                : command.Reason.Trim();

            booking.Cancel(reason);
            _marketplace.Bookings.Update(booking);
            await _corporate.SaveChangesAsync(ct);

            await CacheInvalidation.ForBookingChangeAsync(
                _cache,
                booking.ParkingSpaceId,
                memberId: booking.UserId,
                vendorId: null,
                ct);
            await _quotaCache.InvalidateCompanyAsync(command.CompanyId, ct);

            return new ApiResponse<CorporateBookingDto>(
                true,
                "Corporate booking cancelled.",
                CorporateMapping.ToCorporateBookingDto(corporateBooking, booking));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CorporateBookingDto>(false, ex.Message, null);
        }
    }
}
