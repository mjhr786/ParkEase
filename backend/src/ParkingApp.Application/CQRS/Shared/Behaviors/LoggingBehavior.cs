using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Application.CQRS.Behaviors;

/// <summary>
/// Logs request type and elapsed time for every command/query.
/// </summary>
public sealed class LoggingBehavior : IDispatcherBehavior
{
    private readonly ILogger<LoggingBehavior> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior> logger)
    {
        _logger = logger;
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
        _logger.LogInformation("Handling {Kind} {RequestName}", kind, requestName);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next();
            sw.Stop();
            _logger.LogInformation(
                "Handled {Kind} {RequestName} in {ElapsedMs}ms",
                kind,
                requestName,
                sw.ElapsedMilliseconds);
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
