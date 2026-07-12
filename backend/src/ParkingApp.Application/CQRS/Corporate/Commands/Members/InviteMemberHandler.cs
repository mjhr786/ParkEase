using Microsoft.Extensions.Logging;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Members;

public class InviteMemberHandler : ICommandHandler<InviteMemberCommand, ApiResponse<InvitationDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IIdentityUnitOfWork _identity;
    private readonly IEmailService _email;
    private readonly ICorporateWebLinkBuilder _links;
    private readonly ILogger<InviteMemberHandler> _logger;

    public InviteMemberHandler(
        ICorporateUnitOfWork corporate,
        IIdentityUnitOfWork identity,
        IEmailService email,
        ICorporateWebLinkBuilder links,
        ILogger<InviteMemberHandler> logger)
    {
        _corporate = corporate;
        _identity = identity;
        _email = email;
        _links = links;
        _logger = logger;
    }

    public async Task<ApiResponse<InvitationDto>> HandleAsync(InviteMemberCommand command, CancellationToken ct = default)
    {
        var company = await _corporate.Companies.GetByIdAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<InvitationDto>(false, "Company not found.", null);
        }

        try
        {
            if (!company.IsActive)
            {
                return new ApiResponse<InvitationDto>(false, "This company is inactive.", null);
            }

            var adminMembership = await _corporate.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
            if (adminMembership == null || !adminMembership.IsActive)
            {
                return new ApiResponse<InvitationDto>(false, "You are not an active member of this company.", null);
            }

            if (!adminMembership.IsAdmin)
            {
                return new ApiResponse<InvitationDto>(false, "Only company admins can perform this action.", null);
            }

            var normalizedEmail = command.Dto.Email.Trim().ToLowerInvariant();
            var existingUser = await _identity.Users.GetByEmailAsync(normalizedEmail, ct);
            var emailAlreadyBelongsToMember = existingUser != null
                && await _corporate.Companies.IsUserMemberAsync(command.CompanyId, existingUser.Id, ct);

            if (emailAlreadyBelongsToMember)
            {
                return new ApiResponse<InvitationDto>(false, "This user is already a member of the company.", null);
            }

            if (await _corporate.EmployeeInvitations.HasPendingInvitationAsync(command.CompanyId, normalizedEmail, ct))
            {
                return new ApiResponse<InvitationDto>(false, "There is already a pending invitation for this email address.", null);
            }

            var invitation = EmployeeInvitation.Create(command.CompanyId, normalizedEmail, command.Dto.Role, command.AdminUserId);
            await _corporate.EmployeeInvitations.AddAsync(invitation, ct);
            await _corporate.SaveChangesAsync(ct);

            await CorporateInvitationEmail.TrySendAsync(
                _email,
                _links,
                _logger,
                invitation.Email,
                company.Name,
                invitation.InvitationToken,
                invitation.Role,
                invitation.ExpiresAt,
                ct);

            var dto = new InvitationDto(
                invitation.Id,
                invitation.Email,
                invitation.Role,
                invitation.Status,
                invitation.ExpiresAt,
                invitation.CreatedAt,
                invitation.InvitationToken);

            return new ApiResponse<InvitationDto>(
                true,
                "Invitation created successfully. An email was sent if email delivery is configured; you can also share the invite link.",
                dto);
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<InvitationDto>(false, ex.Message, null);
        }
    }
}
