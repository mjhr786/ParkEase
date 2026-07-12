using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Waitlist;

public class CancelWaitlistEntryHandler : ICommandHandler<CancelWaitlistEntryCommand, ApiResponse<bool>>
{
    private readonly ICorporateUnitOfWork _uow;

    public CancelWaitlistEntryHandler(ICorporateUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<bool>> HandleAsync(CancelWaitlistEntryCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetFullAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<bool>(false, "Company not found.", false);
        }

        try
        {
            company.CancelWaitlistEntry(command.UserId, command.WaitlistEntryId);
            await _uow.SaveChangesAsync(ct);

            return new ApiResponse<bool>(true, "Waitlist entry cancelled.", true);
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<bool>(false, ex.Message, false);
        }
    }
}