using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Bookings;

internal sealed class CancelCorporateBookingHandler
    : ICommandHandler<CancelCorporateBookingCommand, ApiResponse<CorporateBookingDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceBookingService _marketplaceBookings;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public CancelCorporateBookingHandler(
        ICorporateUnitOfWork corporate,
        IMarketplaceBookingService marketplaceBookings,
        ICacheService cache,
        ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _marketplaceBookings = marketplaceBookings;
        _cache = cache;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<CorporateBookingDto>> HandleAsync(
        CancelCorporateBookingCommand command,
        CancellationToken ct = default)
    {
        var membership = await _corporate.Companies.GetMembershipAsync(command.CompanyId, command.UserId, ct);
        if (membership is null || !membership.IsActive)
        {
            return new ApiResponse<CorporateBookingDto>(
                false, "Access denied. You are not an active member of this company.", null);
        }

        var corporateBooking = await _corporate.CorporateBookings.GetByCompanyAndBookingIdAsync(
            command.CompanyId, command.BookingId, ct);
        if (corporateBooking is null)
            return new ApiResponse<CorporateBookingDto>(false, "Corporate booking not found.", null);

        if (!membership.IsAdmin && corporateBooking.MembershipId != membership.Id)
        {
            return new ApiResponse<CorporateBookingDto>(
                false, "You can only cancel your own corporate bookings.", null);
        }

        try
        {
            var reason = string.IsNullOrWhiteSpace(command.Reason)
                ? (membership.IsAdmin ? "Cancelled by company admin" : "Cancelled by employee")
                : command.Reason.Trim();

            var cancelResult = await _marketplaceBookings.CancelAsync(command.BookingId, reason, ct);
            if (!cancelResult.Success || cancelResult.Booking is null)
                return new ApiResponse<CorporateBookingDto>(false, cancelResult.Message, null);

            await _corporate.SaveChangesAsync(ct);

            await CacheInvalidation.ForBookingChangeAsync(
                _cache,
                cancelResult.Booking.ParkingSpaceId,
                memberId: cancelResult.Booking.UserId,
                vendorId: null,
                ct);
            await _quotaCache.InvalidateCompanyAsync(command.CompanyId, ct);

            return new ApiResponse<CorporateBookingDto>(
                true,
                "Corporate booking cancelled.",
                CorporateMapping.ToCorporateBookingDto(corporateBooking, cancelResult.Booking));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CorporateBookingDto>(false, ex.Message, null);
        }
    }
}
