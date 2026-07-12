using ParkingApp.Application.Caching;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.ParkingSpaces;

public class RetireCorporateParkingSpaceHandler : ICommandHandler<RetireCorporateParkingSpaceCommand, ApiResponse<bool>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public RetireCorporateParkingSpaceHandler(
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

    public async Task<ApiResponse<bool>> HandleAsync(RetireCorporateParkingSpaceCommand command, CancellationToken ct = default)
    {
        var membership = await _corporate.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership == null || !membership.IsActive || !membership.IsAdmin)
        {
            return new ApiResponse<bool>(false, "Only company admins can retire company-owned parking.", false);
        }

        var parking = await _marketplace.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, ct);
        if (parking == null || parking.CompanyOwnerId != command.CompanyId || parking.OwnershipType != ParkingSpaceOwnershipType.CompanyOwned)
        {
            return new ApiResponse<bool>(false, "Company-owned parking space not found.", false);
        }

        var company = await _corporate.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        var hasActiveAllocation = company?.Allocations.Any(a =>
            a.ParkingSpaceId == command.ParkingSpaceId &&
            !a.IsDeleted &&
            a.Status is AllocationStatus.Active or AllocationStatus.PendingApproval) == true;

        if (hasActiveAllocation)
        {
            return new ApiResponse<bool>(false, "Deactivate or let active allocations expire before retiring this parking space.", false);
        }

        var hasActiveBookings = await _marketplace.Bookings.HasBlockingBookingsForSpaceAsync(
            command.ParkingSpaceId, DateTime.UtcNow, ct);

        if (hasActiveBookings)
        {
            return new ApiResponse<bool>(false, "Cannot retire parking space with active bookings.", false);
        }

        parking.Retire(command.AdminUserId);
        _marketplace.ParkingSpaces.Update(parking);
        await _corporate.SaveChangesAsync(ct);
        await CacheInvalidation.ForParkingMutationAsync(
            _cache, parking.Id, parking.OwnerId, includeReviews: true, ct);
        await _quotaCache.InvalidateCompanyAsync(command.CompanyId, ct);

        return new ApiResponse<bool>(true, "Company-owned parking space retired.", true);
    }
}
