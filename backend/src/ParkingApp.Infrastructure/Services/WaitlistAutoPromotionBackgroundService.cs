using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Services;

public sealed class WaitlistAutoPromotionOptions
{
    public const string SectionName = "Corporate:WaitlistAutoPromotion";

    /// <summary>When false, background auto-promotion is disabled (manual promote still works).</summary>
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 25;
}

/// <summary>
/// Periodically expires stale waitlist entries and auto-promotes queue heads when slots free up.
/// </summary>
public sealed class WaitlistAutoPromotionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<WaitlistAutoPromotionOptions> _options;
    private readonly ILogger<WaitlistAutoPromotionBackgroundService> _logger;

    public WaitlistAutoPromotionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<WaitlistAutoPromotionOptions> options,
        ILogger<WaitlistAutoPromotionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Waitlist auto-promotion background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            var intervalSeconds = Math.Clamp(opts.PollIntervalSeconds, 10, 3600);
            var batchSize = Math.Clamp(opts.BatchSize, 1, 100);

            try
            {
                if (opts.Enabled)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IWaitlistPromotionService>();
                    var result = await service.ProcessPendingAsync(batchSize, stoppingToken);

                    if (result.Promoted > 0 || result.Expired > 0)
                    {
                        _logger.LogInformation(
                            "Waitlist auto-promotion batch: promoted={Promoted}, expired={Expired}, attempted={Attempted}, skipped={Skipped}",
                            result.Promoted,
                            result.Expired,
                            result.Attempted,
                            result.Skipped);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Waitlist auto-promotion poll failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
