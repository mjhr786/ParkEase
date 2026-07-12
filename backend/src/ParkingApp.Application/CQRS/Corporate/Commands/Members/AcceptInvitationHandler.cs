using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Members;

public class AcceptInvitationHandler : ICommandHandler<AcceptInvitationCommand, ApiResponse<MembershipDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IIdentityUnitOfWork _identity;

    public AcceptInvitationHandler(ICorporateUnitOfWork corporate, IIdentityUnitOfWork identity)
    {
        _corporate = corporate;
        _identity = identity;
    }

    public async Task<ApiResponse<MembershipDto>> HandleAsync(AcceptInvitationCommand command, CancellationToken ct = default)
    {
        var user = await _identity.Users.GetByIdAsync(command.UserId, ct);
        if (user == null)
        {
            return new ApiResponse<MembershipDto>(false, "User not found.", null);
        }

        var company = await _corporate.Companies.GetAggregateForInvitationAcceptanceAsync(command.InvitationToken, command.UserId, ct);
        if (company == null)
        {
            return new ApiResponse<MembershipDto>(false, "Invalid or expired invitation.", null);
        }

        try
        {
            var membership = company.AcceptInvitation(command.InvitationToken, command.UserId, user.Email);
            await _corporate.SaveChangesAsync(ct);

            var dto = new MembershipDto(
                membership.Id,
                user.Id,
                user.FullName,
                user.Email,
                membership.Role,
                membership.EmployeeCode,
                membership.Priority,
                membership.IsActive,
                membership.CreatedAt,
                company.Id);

            return new ApiResponse<MembershipDto>(true, "Invitation accepted. You are now a member.", dto);
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<MembershipDto>(false, ex.Message, null);
        }
    }
}