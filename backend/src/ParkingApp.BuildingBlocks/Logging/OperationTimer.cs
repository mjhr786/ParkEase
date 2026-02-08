using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ParkingApp.BuildingBlocks.Logging;

/// <summary>
/// Scoped timer for measuring operation duration with automatic logging
/// </summary>
public sealed class OperationTimer : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operationName;
    private readonly Stopwatch _stopwatch;
    private readonly LogLevel _logLevel;
    private bool _disposed;

    public OperationTimer(ILogger logger, string operationName, LogLevel logLevel = LogLevel.Information)
    {
        _logger = logger;
        _operationName = operationName;
        _logLevel = logLevel;
        _stopwatch = Stopwatch.StartNew();
        
        _logger.Log(_logLevel, "Starting {Operation}", _operationName);
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Dispose()
    {
        if (_disposed) return;
        
        _stopwatch.Stop();
        _logger.Log(_logLevel, "Completed {Operation} in {ElapsedMs:0.00}ms", _operationName, _stopwatch.Elapsed.TotalMilliseconds);
        _disposed = true;
    }
}

/// <summary>
/// Factory for creating operation timers
/// </summary>
public static class TimerFactory
{
    public static OperationTimer Time(this ILogger logger, string operationName, LogLevel logLevel = LogLevel.Information)
    {
        return new OperationTimer(logger, operationName, logLevel);
    }
    
    public static async Task<T> TimeAsync<T>(this ILogger logger, string operationName, Func<Task<T>> operation, LogLevel logLevel = LogLevel.Information)
    {
        using var timer = new OperationTimer(logger, operationName, logLevel);
        return await operation();
    }
    
    public static async Task TimeAsync(this ILogger logger, string operationName, Func<Task> operation, LogLevel logLevel = LogLevel.Information)
    {
        using var timer = new OperationTimer(logger, operationName, logLevel);
        await operation();
    }
}
