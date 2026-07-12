using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Allocations;

public class AssignFixedSlotHandler : ICommandHandler<AssignFixedSlotCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;

    public AssignFixedSlotHandler(ICorporateUnitOfWork corporate, IMarketplaceUnitOfWork marketplace)
    {
        _corporate = corporate;
        _marketplace = marketplace;
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