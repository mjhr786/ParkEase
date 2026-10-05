using ParkingApp.Application.CQRS;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace ParkingApp.Corporate.Application.Commands.Jobs;

public record PromoteWaitlistCommand(int BatchSize = 25) : ICommand<WaitlistAutoPromotionBatchResult>;

public class PromoteWaitlistCommandHandler : ICommandHandler<PromoteWaitlistCommand, WaitlistAutoPromotionBatchResult>
{
    private readonly IWaitlistPromotionService _service;

    public PromoteWaitlistCommandHandler(IWaitlistPromotionService service)
    {
        _service = service;
    }

    public async Task<WaitlistAutoPromotionBatchResult> HandleAsync(PromoteWaitlistCommand command, CancellationToken cancellationToken = default)
    {
        return await _service.ProcessPendingAsync(command.BatchSize, cancellationToken);
    }
}
