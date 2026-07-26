using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Allocations;

internal sealed class UpdateBookingPolicyHandler
    : ICommandHandler<UpdateBookingPolicyCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly ICompanyQuotaCache _quotaCache;

    public UpdateBookingPolicyHandler(
        ICorporateUnitOfWork corporate,
        IParkingSpaceLookup parkingSpaceLookup,
        ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _parkingSpaceLookup = parkingSpaceLookup;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(
        UpdateBookingPolicyCommand command,
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
            var policy = CorporateCommandHelpers.CreateBookingPolicy(command.Policy) ?? BookingPolicy.Default();
            company.UpdateAllocationPolicy(command.AdminUserId, command.AllocationId, policy);

            await _corporate.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            var parkingSpace = await _parkingSpaceLookup.GetByIdAsync(allocation.ParkingSpaceId, ct);
            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Booking policy updated.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace?.Title ?? string.Empty));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}
