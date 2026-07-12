using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
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
/// Transient DB timeouts (common with remote Supabase from a laptop) are logged as warnings
/// with backoff — they should not look like hard process failures.
/// </summary>
public sealed class WaitlistAutoPromotionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<WaitlistAutoPromotionOptions> _options;
    private readonly ILogger<WaitlistAutoPromotionBackgroundService> _logger;
    private int _consecutiveFailures;

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

        // Avoid racing API startup / first connection pool warm-up.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

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

                    _consecutiveFailures = 0;

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
                _consecutiveFailures++;
                if (IsTransientDbFailure(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Waitlist auto-promotion poll timed out or lost the DB connection (attempt {Attempt}). Will retry with backoff.",
                        _consecutiveFailures);
                }
                else
                {
                    _logger.LogError(ex, "Waitlist auto-promotion poll failed");
                }
            }

            var delaySeconds = intervalSeconds;
            if (_consecutiveFailures > 0)
            {
                // 15s, 30s, 60s… capped at 5 minutes — avoid hammering a flaky link.
                delaySeconds = Math.Min(300, 15 * (1 << Math.Min(_consecutiveFailures - 1, 4)));
                delaySeconds = Math.Max(delaySeconds, intervalSeconds);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static bool IsTransientDbFailure(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException!)
        {
            if (e is TimeoutException)
                return true;
            if (e is NpgsqlException npg)
            {
                var msg = npg.Message ?? string.Empty;
                if (msg.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Exception while reading", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Exception while writing", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("connection is not open", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("Broken", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            if (e is IOException)
                return true;
        }

        return false;
    }
}
