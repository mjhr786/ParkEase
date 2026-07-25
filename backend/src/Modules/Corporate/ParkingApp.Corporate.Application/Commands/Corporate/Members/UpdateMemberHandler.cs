using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Members;

internal sealed class UpdateMemberHandler : ICommandHandler<UpdateMemberCommand, ApiResponse<MembershipDto>>
{
    private readonly ICorporateUnitOfWork _uow;

    public UpdateMemberHandler(ICorporateUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<MembershipDto>> HandleAsync(UpdateMemberCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetWithMembershipsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<MembershipDto>(false, "Company not found.", null);
        }

        if (command.Dto.Role is null
            && command.Dto.Priority is null
            && !command.Dto.ClearEmployeeCode
            && command.Dto.EmployeeCode is null)
        {
            return new ApiResponse<MembershipDto>(false, "No member fields to update.", null);
        }

        try
        {
            var updateCode = command.Dto.ClearEmployeeCode || command.Dto.EmployeeCode is not null;
            var membership = company.UpdateMember(
                command.AdminUserId,
                command.MembershipId,
                command.Dto.Role,
                command.Dto.Priority,
                command.Dto.ClearEmployeeCode ? null : command.Dto.EmployeeCode,
                updateEmployeeCode: updateCode);

            await _uow.SaveChangesAsync(ct);

            // User navigation may not be loaded; fall back to empty strings (list reload fills them).
            var userName = string.Empty;
            var userEmail = string.Empty;

            var dto = new MembershipDto(
                membership.Id,
                membership.UserId,
                userName,
                userEmail,
                membership.Role,
                membership.EmployeeCode,
                membership.Priority,
                membership.IsActive,
                membership.CreatedAt,
                company.Id);

            return new ApiResponse<MembershipDto>(true, "Member updated.", dto);
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<MembershipDto>(false, ex.Message, null);
        }
    }
}



