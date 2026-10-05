using ParkingApp.Application.CQRS;
using ParkingApp.Application.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace ParkingApp.Application.CQRS.Commands.Jobs;

public record ProcessOutboxCommand(int BatchSize = 50) : ICommand<int>;

public class ProcessOutboxCommandHandler : ICommandHandler<ProcessOutboxCommand, int>
{
    private readonly IOutboxProcessor _outboxProcessor;

    public ProcessOutboxCommandHandler(IOutboxProcessor outboxProcessor)
    {
        _outboxProcessor = outboxProcessor;
    }

    public async Task<int> HandleAsync(ProcessOutboxCommand command, CancellationToken cancellationToken = default)
    {
        var take = System.Math.Clamp(command.BatchSize, 1, 200);
        return await _outboxProcessor.ProcessPendingAsync(take, cancellationToken);
    }
}
