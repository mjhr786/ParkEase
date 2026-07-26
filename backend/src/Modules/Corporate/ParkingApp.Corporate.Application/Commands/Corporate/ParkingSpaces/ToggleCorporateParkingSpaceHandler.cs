using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Application.CQRS.Commands.Corporate.ParkingSpaces;

internal sealed class ToggleCorporateParkingSpaceHandler
    : ICommandHandler<ToggleCorporateParkingSpaceCommand, ApiResponse<CorporateParkingSpaceDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly ICompanyOwnedParkingSpaceService _parkingSpaces;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public ToggleCorporateParkingSpaceHandler(
        ICorporateUnitOfWork corporate,
        ICompanyOwnedParkingSpaceService parkingSpaces,
        ICacheService cache,
        ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _parkingSpaces = parkingSpaces;
        _cache = cache;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<CorporateParkingSpaceDto>> HandleAsync(
        ToggleCorporateParkingSpaceCommand command,
        CancellationToken ct = default)
    {
        var membership = await _corporate.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership is null || !membership.IsActive || !membership.IsAdmin)
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Only company admins can update company-owned parking.", null);

        var result = await _parkingSpaces.ToggleActiveAsync(command.CompanyId, command.ParkingSpaceId, ct);
        if (!result.Success || result.Space is null)
            return new ApiResponse<CorporateParkingSpaceDto>(false, result.Message, null);

        await _corporate.SaveChangesAsync(ct);
        await CacheInvalidation.ForParkingMutationAsync(
            _cache, result.Space.Id, result.Space.OwnerId, includeReviews: false, ct);
        await _quotaCache.InvalidateCompanyAsync(command.CompanyId, ct);

        return new ApiResponse<CorporateParkingSpaceDto>(
            true,
            result.Message,
            CorporateMapping.ToCorporateParkingSpaceDto(result.Space));
    }
}
