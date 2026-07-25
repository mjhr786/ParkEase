using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ParkingApp.Application.CQRS.Behaviors;

/// <summary>
/// Logs CQRS request type and elapsed time.
/// Start + fast success paths use Debug; slow successes use Information; failures always Error.
/// </summary>
public sealed class LoggingBehavior : IDispatcherBehavior
{
    private readonly ILogger<LoggingBehavior> _logger;
    private readonly int _slowRequestMs;

    public LoggingBehavior(
        ILogger<LoggingBehavior> logger,
        IOptions<PerformanceLoggingOptions>? options = null)
    {
        _logger = logger;
        var ms = options?.Value?.SlowRequestMs ?? 200;
        _slowRequestMs = ms < 0 ? 0 : ms;
    }

    public int Order => 0;

    public async Task<TResult> HandleAsync<TResult>(
        object request,
        bool isCommand,
        RequestHandlerDelegate<TResult> next,
        CancellationToken cancellationToken)
    {
        var requestName = request.GetType().Name;
        var kind = isCommand ? "Command" : "Query";
        _logger.LogDebug("Handling {Kind} {RequestName}", kind, requestName);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds >= _slowRequestMs)
            {
                _logger.LogInformation(
                    "Handled {Kind} {RequestName} in {ElapsedMs}ms",
                    kind,
                    requestName,
                    sw.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogDebug(
                    "Handled {Kind} {RequestName} in {ElapsedMs}ms",
                    kind,
                    requestName,
                    sw.ElapsedMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "Failed {Kind} {RequestName} after {ElapsedMs}ms",
                kind,
                requestName,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
