namespace ParkingApp.Marketplace.Application.Interfaces;

public interface ISessionReminderService
{
    Task<SessionReminderResult> ProcessAsync(int batchSize, CancellationToken cancellationToken = default);
}

public sealed record SessionReminderResult(int Notified, int Examined);
