using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Companies;

public class CreateCompanyHandler : ICommandHandler<CreateCompanyCommand, ApiResponse<CompanyDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IIdentityUnitOfWork _identity;

    public CreateCompanyHandler(ICorporateUnitOfWork corporate, IIdentityUnitOfWork identity)
    {
        _corporate = corporate;
        _identity = identity;
    }

    public async Task<ApiResponse<CompanyDto>> HandleAsync(CreateCompanyCommand command, CancellationToken ct = default)
    {
        var user = await _identity.Users.GetByIdAsync(command.UserId, ct);
        if (user == null)
        {
            return new ApiResponse<CompanyDto>(false, "User not found.", null);
        }

        if (await _corporate.Companies.ExistsByRegistrationNumberAsync(command.Dto.RegistrationNumber, ct))
        {
            return new ApiResponse<CompanyDto>(false, "A company with this registration number already exists.", null);
        }

        var company = Company.Create(
            command.Dto.Name,
            command.Dto.RegistrationNumber,
            command.Dto.ContactEmail,
            command.Dto.ContactPhone,
            command.Dto.BillingAddress,
            command.Dto.BillingType,
            command.UserId);

        await _corporate.Companies.AddAsync(company, ct);
        await _corporate.SaveChangesAsync(ct);

        return new ApiResponse<CompanyDto>(true, "Company created successfully.", CorporateMapping.ToCompanyDto(company));
    }
}