using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace ParkingApp.Marketplace.Application.Commands.Jobs;

public record DetectOverstaysCommand(int BatchSize = 50) : ICommand<OverstayDetectionResult>;

public class DetectOverstaysCommandHandler : ICommandHandler<DetectOverstaysCommand, OverstayDetectionResult>
{
    private readonly IOverstayDetectionService _service;

    public DetectOverstaysCommandHandler(IOverstayDetectionService service)
    {
        _service = service;
    }

    public async Task<OverstayDetectionResult> HandleAsync(DetectOverstaysCommand command, CancellationToken cancellationToken = default)
    {
        return await _service.ProcessAsync(command.BatchSize, cancellationToken);
    }
}
