using ParkingApp.Application.Caching;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Application.CQRS.Commands.Corporate.ParkingSpaces;

internal sealed class RetireCorporateParkingSpaceHandler
    : ICommandHandler<RetireCorporateParkingSpaceCommand, ApiResponse<bool>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly ICompanyOwnedParkingSpaceService _parkingSpaces;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public RetireCorporateParkingSpaceHandler(
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

    public async Task<ApiResponse<bool>> HandleAsync(
        RetireCorporateParkingSpaceCommand command,
        CancellationToken ct = default)
    {
        var membership = await _corporate.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership is null || !membership.IsActive || !membership.IsAdmin)
            return new ApiResponse<bool>(false, "Only company admins can retire company-owned parking.", false);

        var company = await _corporate.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        var hasActiveAllocation = company?.Allocations.Any(a =>
            a.ParkingSpaceId == command.ParkingSpaceId &&
            !a.IsDeleted &&
            a.Status is AllocationStatus.Active or AllocationStatus.PendingApproval) == true;

        if (hasActiveAllocation)
        {
            return new ApiResponse<bool>(
                false,
                "Deactivate or let active allocations expire before retiring this parking space.",
                false);
        }

        var result = await _parkingSpaces.RetireAsync(
            command.CompanyId, command.ParkingSpaceId, command.AdminUserId, ct);
        if (!result.Success)
            return new ApiResponse<bool>(false, result.Message, false);

        await _corporate.SaveChangesAsync(ct);
        if (result.Space is not null)
        {
            await CacheInvalidation.ForParkingMutationAsync(
                _cache, result.Space.Id, result.Space.OwnerId, includeReviews: true, ct);
        }

        await _quotaCache.InvalidateCompanyAsync(command.CompanyId, ct);
        return new ApiResponse<bool>(true, result.Message, true);
    }
}
