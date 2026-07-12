using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Allocations;

public class UpdateAllocationContractHandler : ICommandHandler<UpdateAllocationContractCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly IIdentityUnitOfWork _identity;
    private readonly ICompanyQuotaCache _quotaCache;

    public UpdateAllocationContractHandler(ICorporateUnitOfWork corporate, IMarketplaceUnitOfWork marketplace, IIdentityUnitOfWork identity, ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _marketplace = marketplace;
        _identity = identity;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(UpdateAllocationContractCommand command, CancellationToken ct = default)
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
            company.UpdateAllocationContract(
                command.AdminUserId,
                command.AllocationId,
                command.Dto.MonthlyRate,
                command.Dto.StartDate,
                command.Dto.EndDate,
                command.Dto.LeaseReference);

            await _corporate.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            var parkingSpace = await _marketplace.ParkingSpaces.GetByIdAsync(allocation.ParkingSpaceId, ct);
            string? vendorName = null;
            if (allocation.VendorId.HasValue)
            {
                var vendor = await _identity.Users.GetByIdAsync(allocation.VendorId.Value, ct);
                if (vendor != null)
                {
                    vendorName = $"{vendor.FirstName} {vendor.LastName}".Trim();
                }
            }

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Allocation contract terms updated.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace?.Title ?? string.Empty, vendorName));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}
