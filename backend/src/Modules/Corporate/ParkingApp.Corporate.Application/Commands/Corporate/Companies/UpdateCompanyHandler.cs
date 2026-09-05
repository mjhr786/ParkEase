using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Companies;

internal sealed class UpdateCompanyHandler : ICommandHandler<UpdateCompanyCommand, ApiResponse<CompanyDto>>
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
            && dto.BillingType is null
            && dto.Slug is null)
        {
            return new ApiResponse<CompanyDto>(false, "No company fields to update.", null, null, "validation_failed");
        }

        try
        {
            if (dto.Name is not null
                || dto.ContactEmail is not null
                || dto.ContactPhone is not null
                || dto.BillingAddress is not null
                || dto.BillingType is not null)
            {
                company.UpdateProfile(
                    command.AdminUserId,
                    dto.Name,
                    dto.ContactEmail,
                    dto.ContactPhone,
                    dto.BillingAddress,
                    dto.BillingType);
            }

            if (dto.Slug is not null)
            {
                var normalized = dto.Slug.Trim().ToLowerInvariant();
                if (await _uow.Companies.ExistsBySlugAsync(normalized, command.CompanyId, ct))
                {
                    return new ApiResponse<CompanyDto>(
                        false,
                        "This company slug is already in use.",
                        null,
                        new List<string> { "slug_taken" },
                        "slug_taken");
                }

                company.SetSlug(normalized);
            }

            await _uow.SaveChangesAsync(ct);

            return new ApiResponse<CompanyDto>(
                true,
                "Company profile updated.",
                CorporateMapping.ToCompanyDto(company));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CompanyDto>(false, ex.Message, null, null, "validation_failed");
        }
    }
}
