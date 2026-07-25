using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Waitlist;

internal sealed class PromoteWaitlistEntryHandler : ICommandHandler<PromoteWaitlistEntryCommand, ApiResponse<CorporateReservationResultDto>>
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
