using Microsoft.Extensions.Logging;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Members;

public class ResendInvitationHandler : ICommandHandler<ResendInvitationCommand, ApiResponse<InvitationDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly IEmailService _email;
    private readonly ICorporateWebLinkBuilder _links;
    private readonly ILogger<ResendInvitationHandler> _logger;

    public ResendInvitationHandler(
        ICorporateUnitOfWork uow,
        IEmailService email,
        ICorporateWebLinkBuilder links,
        ILogger<ResendInvitationHandler> logger)
    {
        _uow = uow;
        _email = email;
        _links = links;
        _logger = logger;
    }

    public async Task<ApiResponse<InvitationDto>> HandleAsync(ResendInvitationCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetFullAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<InvitationDto>(false, "Company not found.", null);
        }

        try
        {
            var invitation = company.ResendInvitation(command.AdminUserId, command.InvitationId);
            await _uow.SaveChangesAsync(ct);

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
                "Invitation resent with a new link and expiry. Email sent if delivery is configured.",
                dto);
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<InvitationDto>(false, ex.Message, null);
        }
    }
}
