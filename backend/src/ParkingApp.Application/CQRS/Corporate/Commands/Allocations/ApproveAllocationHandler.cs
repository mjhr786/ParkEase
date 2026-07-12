using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Allocations;

public class ApproveAllocationHandler : ICommandHandler<ApproveAllocationCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly ICompanyQuotaCache _quotaCache;

    public ApproveAllocationHandler(ICorporateUnitOfWork corporate, IMarketplaceUnitOfWork marketplace, ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _marketplace = marketplace;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(ApproveAllocationCommand command, CancellationToken ct = default)
    {
        var company = await _corporate.Companies.GetAggregateByAllocationAsync(command.AllocationId, ct);
        if (company == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);
        }

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.AllocationId && !a.IsDeleted);
        if (allocation == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);
        }

        var parkingSpace = await _marketplace.ParkingSpaces.GetByIdAsync(allocation.ParkingSpaceId, ct);
        if (parkingSpace == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Parking space not found.", null);
        }

        if (allocation.SourceType != ParkingAllocationSource.VendorLease)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Only vendor-leased allocations require vendor approval.", null);
        }

        if (parkingSpace.OwnerId != command.ParkingOwnerUserId)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Only the parking space owner can approve allocations.", null);
        }

        try
        {
            company.ApproveAllocation(command.AllocationId, command.ParkingOwnerUserId);
            await _corporate.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Allocation approved.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace.Title));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}