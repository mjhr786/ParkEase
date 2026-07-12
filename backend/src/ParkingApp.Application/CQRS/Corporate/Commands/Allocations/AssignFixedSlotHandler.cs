using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Allocations;

public class AssignFixedSlotHandler : ICommandHandler<AssignFixedSlotCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly ICompanyQuotaCache _quotaCache;
    private readonly ICacheService _cache;

    public AssignFixedSlotHandler(
        ICorporateUnitOfWork corporate,
        IMarketplaceUnitOfWork marketplace,
        ICompanyQuotaCache quotaCache,
        ICacheService cache)
    {
        _corporate = corporate;
        _marketplace = marketplace;
        _quotaCache = quotaCache;
        _cache = cache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(AssignFixedSlotCommand command, CancellationToken ct = default)
    {
        var company = await _corporate.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Company not found.", null);
        }

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.AllocationId && !a.IsDeleted);
        if (allocation == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);
        }

        try
        {
            company.AssignFixedSlot(command.AdminUserId, command.AllocationId, command.Dto.MembershipId, command.Dto.SlotNumber);
            await _corporate.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(command.CompanyId, ct);
            await CacheInvalidation.ForCompanyDashboardAsync(_cache, command.CompanyId, ct);

            var parkingSpace = await _marketplace.ParkingSpaces.GetByIdAsync(allocation.ParkingSpaceId, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Fixed slot assigned.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace?.Title ?? string.Empty));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}