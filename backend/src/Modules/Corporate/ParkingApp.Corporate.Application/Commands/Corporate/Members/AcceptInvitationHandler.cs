using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Identity.Contracts;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Members;

internal sealed class AcceptInvitationHandler : ICommandHandler<AcceptInvitationCommand, ApiResponse<MembershipDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IUserLookup _users;

    public AcceptInvitationHandler(ICorporateUnitOfWork corporate, IUserLookup users)
    {
        _corporate = corporate;
        _users = users;
    }

    public async Task<ApiResponse<MembershipDto>> HandleAsync(AcceptInvitationCommand command, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(command.UserId, ct);
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
                user.UserId,
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
