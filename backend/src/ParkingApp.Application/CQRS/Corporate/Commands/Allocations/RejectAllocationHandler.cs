using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Allocations;

public class RejectAllocationHandler : ICommandHandler<RejectAllocationCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly ICompanyQuotaCache _quotaCache;

    public RejectAllocationHandler(ICorporateUnitOfWork corporate, IMarketplaceUnitOfWork marketplace, ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _marketplace = marketplace;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(RejectAllocationCommand command, CancellationToken ct = default)
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
            return new ApiResponse<ParkingAllocationDto>(false, "Only vendor-leased allocations can be rejected by a parking owner.", null);
        }

        if (parkingSpace.OwnerId != command.ParkingOwnerUserId)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Only the parking space owner can reject allocations.", null);
        }

        try
        {
            company.RejectAllocation(command.AllocationId, command.Reason);
            await _corporate.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Allocation rejected.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace.Title));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}