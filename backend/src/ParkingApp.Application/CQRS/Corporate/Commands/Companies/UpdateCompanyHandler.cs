using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Companies;

public class UpdateCompanyHandler : ICommandHandler<UpdateCompanyCommand, ApiResponse<CompanyDto>>
{
    private readonly ICorporateUnitOfWork _uow;

    public UpdateCompanyHandler(ICorporateUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<CompanyDto>> HandleAsync(UpdateCompanyCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetWithMembershipsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<CompanyDto>(false, "Company not found.", null);
        }

        var dto = command.Dto;
        if (dto.Name is null
            && dto.ContactEmail is null
            && dto.ContactPhone is null
            && dto.BillingAddress is null
            && dto.BillingType is null)
        {
            return new ApiResponse<CompanyDto>(false, "No company fields to update.", null);
        }

        try
        {
            company.UpdateProfile(
                command.AdminUserId,
                dto.Name,
                dto.ContactEmail,
                dto.ContactPhone,
                dto.BillingAddress,
                dto.BillingType);

            await _uow.SaveChangesAsync(ct);

            return new ApiResponse<CompanyDto>(
                true,
                "Company profile updated.",
                CorporateMapping.ToCompanyDto(company));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CompanyDto>(false, ex.Message, null);
        }
    }
}
