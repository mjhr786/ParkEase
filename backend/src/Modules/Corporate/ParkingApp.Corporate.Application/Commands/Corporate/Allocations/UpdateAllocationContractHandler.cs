using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Allocations;

internal sealed class UpdateAllocationContractHandler
    : ICommandHandler<UpdateAllocationContractCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _users;
    private readonly ICompanyQuotaCache _quotaCache;

    public UpdateAllocationContractHandler(
        ICorporateUnitOfWork corporate,
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup users,
        ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _parkingSpaceLookup = parkingSpaceLookup;
        _users = users;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(
        UpdateAllocationContractCommand command,
        CancellationToken ct = default)
    {
        var company = await _corporate.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        if (company is null)
            return new ApiResponse<ParkingAllocationDto>(false, "Company not found.", null);

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.AllocationId && !a.IsDeleted);
        if (allocation is null)
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);

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

            var parkingSpace = await _parkingSpaceLookup.GetByIdAsync(allocation.ParkingSpaceId, ct);
            string? vendorName = null;
            if (allocation.VendorId.HasValue)
            {
                var vendor = await _users.GetByIdAsync(allocation.VendorId.Value, ct);
                if (vendor is not null)
                    vendorName = $"{vendor.FirstName} {vendor.LastName}".Trim();
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
