using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Waitlist;

public class PromoteWaitlistEntryHandler : ICommandHandler<PromoteWaitlistEntryCommand, ApiResponse<CorporateReservationResultDto>>
{
    private readonly IWaitlistPromotionService _promotionService;

    public PromoteWaitlistEntryHandler(IWaitlistPromotionService promotionService)
    {
        _promotionService = promotionService;
    }

    public Task<ApiResponse<CorporateReservationResultDto>> HandleAsync(
        PromoteWaitlistEntryCommand command,
        CancellationToken ct = default)
    {
        return _promotionService.PromoteAsync(
            command.CompanyId,
            command.WaitlistEntryId,
            adminUserId: command.AdminUserId,
            cancellationToken: ct);
    }
}
