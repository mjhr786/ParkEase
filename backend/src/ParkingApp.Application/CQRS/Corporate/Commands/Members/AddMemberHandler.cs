using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Members;

public class AddMemberHandler : ICommandHandler<AddMemberCommand, ApiResponse<MembershipDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IIdentityUnitOfWork _identity;

    public AddMemberHandler(ICorporateUnitOfWork corporate, IIdentityUnitOfWork identity)
    {
        _corporate = corporate;
        _identity = identity;
    }

    public async Task<ApiResponse<MembershipDto>> HandleAsync(AddMemberCommand command, CancellationToken ct = default)
    {
        var targetUser = await _identity.Users.GetByEmailAsync(command.Dto.Email, ct);
        if (targetUser == null)
        {
            return new ApiResponse<MembershipDto>(false, "No ParkEase user found with this email. Please invite them instead.", null);
        }

        var company = await _corporate.Companies.GetWithMembershipsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<MembershipDto>(false, "Company not found.", null);
        }

        try
        {
            var membership = company.AddMember(
                command.AdminUserId,
                targetUser.Id,
                command.Dto.Role,
                command.Dto.EmployeeCode,
                command.Dto.Priority);

            await _corporate.SaveChangesAsync(ct);

            var dto = new MembershipDto(
                membership.Id,
                targetUser.Id,
                targetUser.FullName,
                targetUser.Email,
                membership.Role,
                membership.EmployeeCode,
                membership.Priority,
                membership.IsActive,
                membership.CreatedAt,
                company.Id);

            return new ApiResponse<MembershipDto>(true, "Member added successfully.", dto);
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<MembershipDto>(false, ex.Message, null);
        }
    }
}