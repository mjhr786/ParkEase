using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Members;

public class RemoveMemberHandler : ICommandHandler<RemoveMemberCommand, ApiResponse<bool>>
{
    private readonly ICorporateUnitOfWork _uow;

    public RemoveMemberHandler(ICorporateUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<bool>> HandleAsync(RemoveMemberCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetWithMembershipsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<bool>(false, "Company not found.", false);
        }

        try
        {
            company.RemoveMember(command.AdminUserId, command.MembershipId);
            await _uow.SaveChangesAsync(ct);

            return new ApiResponse<bool>(true, "Member removed successfully.", true);
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<bool>(false, ex.Message, false);
        }
    }
}