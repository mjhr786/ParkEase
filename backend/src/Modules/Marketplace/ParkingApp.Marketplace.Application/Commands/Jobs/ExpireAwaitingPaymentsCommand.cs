using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.Jobs;

public sealed record ExpireAwaitingPaymentsCommand(int BatchSize = 50)
    : ICommand<ExpireAwaitingPaymentsResult>;

internal sealed class ExpireAwaitingPaymentsCommandHandler
    : ICommandHandler<ExpireAwaitingPaymentsCommand, ExpireAwaitingPaymentsResult>
{
    private readonly IUnpaidExpiryService _service;

    public ExpireAwaitingPaymentsCommandHandler(IUnpaidExpiryService service) => _service = service;

    public Task<ExpireAwaitingPaymentsResult> HandleAsync(
        ExpireAwaitingPaymentsCommand command,
        CancellationToken cancellationToken = default) =>
        _service.RunJobAsync(command.BatchSize, cancellationToken);
}
