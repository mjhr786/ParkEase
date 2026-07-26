using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;

namespace ParkingApp.Marketplace.Infrastructure.Services;

/// <summary>Polls for overstaying InProgress bookings and sends one-time alerts.</summary>
internal sealed class OverstayDetectionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<LprAccessOptions> _options;
    private readonly ILogger<OverstayDetectionBackgroundService> _logger;

    public OverstayDetectionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<LprAccessOptions> options,
        ILogger<OverstayDetectionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LPR overstay detection background service started");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var overstay = _options.CurrentValue.Overstay;
            var interval = Math.Clamp(overstay.PollIntervalSeconds, 30, 3600);
            var batch = Math.Clamp(overstay.BatchSize, 1, 200);

            try
            {
                if (overstay.Enabled)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IOverstayDetectionService>();
                    var result = await service.ProcessAsync(batch, stoppingToken);

                    if (result.Notified > 0 || result.FeesAssessed > 0 || result.AutoCheckedOut > 0)
                    {
                        _logger.LogInformation(
                            "Overstay detection: notified={Notified}, fees={Fees}, autoCheckOut={Auto}, examined={Examined}",
                            result.Notified, result.FeesAssessed, result.AutoCheckedOut, result.Examined);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Overstay detection cycle failed");
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
