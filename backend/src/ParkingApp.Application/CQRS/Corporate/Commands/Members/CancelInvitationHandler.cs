using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Members;

public class CancelInvitationHandler : ICommandHandler<CancelInvitationCommand, ApiResponse<bool>>
{
    private readonly ICorporateUnitOfWork _uow;

    public CancelInvitationHandler(ICorporateUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<bool>> HandleAsync(CancelInvitationCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetFullAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<bool>(false, "Company not found.", false);
        }

        try
        {
            company.CancelInvitation(command.AdminUserId, command.InvitationId);
            await _uow.SaveChangesAsync(ct);
            return new ApiResponse<bool>(true, "Invitation cancelled.", true);
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<bool>(false, ex.Message, false);
        }
    }
}
