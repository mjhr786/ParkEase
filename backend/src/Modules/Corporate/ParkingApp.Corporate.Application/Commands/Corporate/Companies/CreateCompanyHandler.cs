using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Identity.Contracts;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Companies;

internal sealed class CreateCompanyHandler : ICommandHandler<CreateCompanyCommand, ApiResponse<CompanyDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IUserLookup _users;

    public CreateCompanyHandler(ICorporateUnitOfWork corporate, IUserLookup users)
    {
        _corporate = corporate;
        _users = users;
    }

    public async Task<ApiResponse<CompanyDto>> HandleAsync(CreateCompanyCommand command, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(command.UserId, ct);
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
