using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.ParkingSpaces;

public class ToggleCorporateParkingSpaceHandler : ICommandHandler<ToggleCorporateParkingSpaceCommand, ApiResponse<CorporateParkingSpaceDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public ToggleCorporateParkingSpaceHandler(
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

    public async Task<ApiResponse<CorporateParkingSpaceDto>> HandleAsync(ToggleCorporateParkingSpaceCommand command, CancellationToken ct = default)
    {
        var membership = await _corporate.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership == null || !membership.IsActive || !membership.IsAdmin)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Only company admins can update company-owned parking.", null);
        }

        var parking = await _marketplace.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, ct);
        if (parking == null || parking.CompanyOwnerId != command.CompanyId || parking.OwnershipType != ParkingSpaceOwnershipType.CompanyOwned)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Company-owned parking space not found.", null);
        }

        parking.ToggleActive();
        _marketplace.ParkingSpaces.Update(parking);
        await _corporate.SaveChangesAsync(ct);
        await CacheInvalidation.ForParkingMutationAsync(_cache, parking.Id, parking.OwnerId, includeReviews: false, ct);
        await _quotaCache.InvalidateCompanyAsync(command.CompanyId, ct);

        return new ApiResponse<CorporateParkingSpaceDto>(
            true,
            parking.IsActive ? "Parking space activated." : "Parking space deactivated.",
            CorporateMapping.ToCorporateParkingSpaceDto(parking, command.CompanyId));
    }
}
