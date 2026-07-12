using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Allocations;

public class AllocateParkingSlotsHandler : ICommandHandler<AllocateParkingSlotsCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly ICompanyQuotaCache _quotaCache;

    public AllocateParkingSlotsHandler(ICorporateUnitOfWork corporate, IMarketplaceUnitOfWork marketplace, ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _marketplace = marketplace;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(AllocateParkingSlotsCommand command, CancellationToken ct = default)
    {
        var company = await _corporate.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Company not found.", null);
        }

        var parkingSpace = await _marketplace.ParkingSpaces.GetByIdAsync(command.Dto.ParkingSpaceId, ct);
        if (parkingSpace == null || !parkingSpace.IsActive)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Parking space not found or inactive.", null);
        }

        if (parkingSpace.OwnershipType == ParkingSpaceOwnershipType.CompanyOwned)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Company-owned parking must use the internal corporate allocation flow.", null);
        }

        try
        {
            var quota = Quota.Create(command.Dto.TotalSlots, command.Dto.FixedSlots, command.Dto.SharedSlots);
            var policy = CorporateCommandHelpers.CreateBookingPolicy(command.Dto.Policy);

            var allocation = company.RequestAllocation(
                command.AdminUserId,
                command.Dto.ParkingSpaceId,
                quota,
                command.Dto.MonthlyRate,
                command.Dto.StartDate,
                command.Dto.EndDate,
                parkingSpace.TotalSpots,
                policy);

            allocation.SetVendorLeaseMetadata(parkingSpace.OwnerId, command.Dto.LeaseReference);

            await _corporate.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Parking allocation created. Awaiting parking space owner approval.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace.Title));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}