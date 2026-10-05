using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace ParkingApp.Marketplace.Application.Commands.Jobs;

public record SendSessionRemindersCommand(int BatchSize = 50) : ICommand<SessionReminderResult>;

public class SendSessionRemindersCommandHandler : ICommandHandler<SendSessionRemindersCommand, SessionReminderResult>
{
    private readonly ISessionReminderService _service;

    public SendSessionRemindersCommandHandler(ISessionReminderService service)
    {
        _service = service;
    }

    public async Task<SessionReminderResult> HandleAsync(SendSessionRemindersCommand command, CancellationToken cancellationToken = default)
    {
        return await _service.ProcessAsync(command.BatchSize, cancellationToken);
    }
}
