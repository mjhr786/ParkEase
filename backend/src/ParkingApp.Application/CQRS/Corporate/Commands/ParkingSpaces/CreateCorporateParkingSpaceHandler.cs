using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.ParkingSpaces;

public class CreateCorporateParkingSpaceHandler : ICommandHandler<CreateCorporateParkingSpaceCommand, ApiResponse<CorporateParkingSpaceDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly IIdentityUnitOfWork _identity;

    public CreateCorporateParkingSpaceHandler(ICorporateUnitOfWork corporate, IMarketplaceUnitOfWork marketplace, IIdentityUnitOfWork identity)
    {
        _corporate = corporate;
        _marketplace = marketplace;
        _identity = identity;
    }

    public async Task<ApiResponse<CorporateParkingSpaceDto>> HandleAsync(CreateCorporateParkingSpaceCommand command, CancellationToken ct = default)
    {
        var company = await _corporate.Companies.GetWithMembershipsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Company not found.", null);
        }

        var adminMembership = company.Memberships.FirstOrDefault(m => m.UserId == command.AdminUserId && !m.IsDeleted);
        if (adminMembership == null || !adminMembership.IsActive || !adminMembership.IsAdmin)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Only company admins can create company-owned parking.", null);
        }

        var adminUser = await _identity.Users.GetByIdAsync(command.AdminUserId, ct);
        if (adminUser == null)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Admin user not found.", null);
        }

        var parking = command.Dto.ToCompanyEntity(command.AdminUserId, command.CompanyId);
        parking.AssignOwnerNavigation(adminUser);

        await _marketplace.ParkingSpaces.AddAsync(parking, ct);
        await _corporate.SaveChangesAsync(ct);

        return new ApiResponse<CorporateParkingSpaceDto>(
            true,
            "Company-owned parking space created.",
            CorporateMapping.ToCorporateParkingSpaceDto(parking, command.CompanyId));
    }
}