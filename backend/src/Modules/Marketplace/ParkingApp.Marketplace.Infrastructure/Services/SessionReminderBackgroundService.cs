using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;

namespace ParkingApp.Marketplace.Infrastructure.Services;

/// <summary>Polls for bookings nearing EndDateTime and sends one-time extend/checkout reminders.</summary>
internal sealed class SessionReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SessionReminderOptions> _options;
    private readonly ILogger<SessionReminderBackgroundService> _logger;

    public SessionReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SessionReminderOptions> options,
        ILogger<SessionReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session end reminder background service started");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            var interval = Math.Clamp(opts.PollIntervalSeconds, 30, 3600);
            var batch = Math.Clamp(opts.BatchSize, 1, 200);

            try
            {
                if (opts.Enabled)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ISessionReminderService>();
                    var result = await service.ProcessAsync(batch, stoppingToken);

                    if (result.Notified > 0)
                    {
                        _logger.LogInformation(
                            "Session reminders: notified={Notified}, examined={Examined}",
                            result.Notified,
                            result.Examined);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Session reminder cycle failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
