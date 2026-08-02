using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Allocations;

internal sealed class CreateOwnedParkingAllocationHandler
    : ICommandHandler<CreateOwnedParkingAllocationCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly ICompanyQuotaCache _quotaCache;

    public CreateOwnedParkingAllocationHandler(
        ICorporateUnitOfWork corporate,
        IParkingSpaceLookup parkingSpaceLookup,
        ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _parkingSpaceLookup = parkingSpaceLookup;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(
        CreateOwnedParkingAllocationCommand command,
        CancellationToken ct = default)
    {
        var company = await _corporate.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        if (company is null)
            return new ApiResponse<ParkingAllocationDto>(false, "Company not found.", null);

        var parkingSpace = await _parkingSpaceLookup.GetByIdAsync(command.Dto.ParkingSpaceId, ct);
        if (parkingSpace is null || !parkingSpace.IsActive)
            return new ApiResponse<ParkingAllocationDto>(false, "Company-owned parking space not found or inactive.", null);

        if (parkingSpace.CompanyOwnerId != command.CompanyId || !parkingSpace.IsCompanyOwned)
            return new ApiResponse<ParkingAllocationDto>(false, "This parking space is not owned by the selected company.", null);

        try
        {
            var (twoWheeler, fourWheeler) = CorporateCommandHelpers.ResolveClassQuotas(
                command.Dto.TwoWheeler,
                command.Dto.FourWheeler,
                command.Dto.TotalSlots,
                command.Dto.FixedSlots,
                command.Dto.SharedSlots);
            var policy = CorporateCommandHelpers.CreateBookingPolicy(command.Dto.Policy);

            var allocation = company.CreateOwnedParkingAllocation(
                command.AdminUserId,
                command.Dto.ParkingSpaceId,
                twoWheeler,
                fourWheeler,
                command.Dto.MonthlyRate,
                command.Dto.StartDate,
                command.Dto.EndDate,
                parkingSpace.TotalSpots,
                policy);

            await _corporate.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Company-owned parking allocation activated.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace.Title));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}
