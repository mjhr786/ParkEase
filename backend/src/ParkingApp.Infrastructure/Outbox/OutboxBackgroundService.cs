using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Outbox;

/// <summary>
/// Polls the outbox for pending/failed messages (retry after transient handler failures).
/// Uses adaptive backoff when empty to reduce free-tier DB connection churn.
/// </summary>
public sealed class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OutboxOptions> _options;
    private readonly ILogger<OutboxBackgroundService> _logger;
    private int _emptyStreak;

    public OutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OutboxOptions> options,
        ILogger<OutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox background processor started");

        // Avoid racing API startup / first connection pool warm-up.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            var batchSize = Math.Clamp(opts.BatchSize, 1, 200);
            var processed = 0;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                processed = await processor.ProcessPendingAsync(batchSize, stoppingToken);
                if (processed > 0)
                    _logger.LogDebug("Outbox background processed {Count} message(s)", processed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox background poll failed");
            }

            var delay = ResolveDelaySeconds(opts, processed);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Busy path stays responsive; empty path backs off to cut idle DB opens under Pooling=false.
    /// Public for unit tests of free-tier cadence math.
    /// </summary>
    public int ResolveDelaySeconds(OutboxOptions opts, int processedCount)
    {
        var busy = Math.Clamp(opts.BusyPollIntervalSeconds, 2, 120);
        var idleBase = Math.Clamp(opts.PollIntervalSeconds, 5, 300);
        var idleMax = Math.Clamp(opts.EmptyBackoffMaxSeconds, idleBase, 600);

        if (processedCount > 0)
        {
            _emptyStreak = 0;
            return busy;
        }

        _emptyStreak++;
        // idleBase, 2x, 4x, 8x, 16x ... capped at idleMax (and not below idleBase).
        var factor = 1 << Math.Min(_emptyStreak - 1, 4);
        var delay = idleBase * factor;
        if (delay > idleMax)
            delay = idleMax;
        return delay;
    }
}
