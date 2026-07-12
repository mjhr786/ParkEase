using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Allocations;

public class RemoveFixedSlotHandler : ICommandHandler<RemoveFixedSlotCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;

    public RemoveFixedSlotHandler(ICorporateUnitOfWork corporate, IMarketplaceUnitOfWork marketplace)
    {
        _corporate = corporate;
        _marketplace = marketplace;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(RemoveFixedSlotCommand command, CancellationToken ct = default)
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

        var adminMembership = company.Memberships.FirstOrDefault(m => m.UserId == command.AdminUserId && !m.IsDeleted);
        if (adminMembership == null || !adminMembership.IsActive || !adminMembership.IsAdmin)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Only company admins can perform this action.", null);
        }

        try
        {
            allocation.RemoveFixedAssignment(command.MembershipId);
            await _corporate.SaveChangesAsync(ct);

            var parkingSpace = await _marketplace.ParkingSpaces.GetByIdAsync(allocation.ParkingSpaceId, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Fixed slot assignment removed.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace?.Title ?? string.Empty));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}