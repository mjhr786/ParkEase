using System.Net;
using System.Text.Json;
using ParkingApp.Application.DTOs;

namespace ParkingApp.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized access"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An error occurred. Please try again later.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new ApiResponse<object>(false, message, null, new List<string> { message });
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Queue<DateTime>> _requestTimes = new();
    private static readonly Timer _cleanupTimer;
    private const int MaxRequests = 100;
    private const int WindowSeconds = 60;
    private const int CleanupIntervalMinutes = 5;

    static RateLimitingMiddleware()
    {
        // Periodic cleanup to prevent memory leaks
        _cleanupTimer = new Timer(CleanupOldEntries, null, 
            TimeSpan.FromMinutes(CleanupIntervalMinutes), 
            TimeSpan.FromMinutes(CleanupIntervalMinutes));
    }

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        if (!IsRequestAllowed(clientId))
        {
            _logger.LogWarning("Rate limit exceeded for client: {ClientId}", clientId);
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.Headers.Append("Retry-After", "60");
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>(
                false, "Rate limit exceeded. Please try again later.", null));
            return;
        }

        await _next(context);
    }

    private static bool IsRequestAllowed(string clientId)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddSeconds(-WindowSeconds);

        var queue = _requestTimes.GetOrAdd(clientId, _ => new Queue<DateTime>());

        lock (queue)
        {
            // Remove old requests
            while (queue.Count > 0 && queue.Peek() < windowStart)
            {
                queue.Dequeue();
            }

            if (queue.Count >= MaxRequests)
            {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }

    private static void CleanupOldEntries(object? state)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-10); // Remove entries older than 10 minutes

        foreach (var key in _requestTimes.Keys.ToList())
        {
            if (_requestTimes.TryGetValue(key, out var queue))
            {
                lock (queue)
                {
                    // If all requests in queue are old, remove the entry
                    if (queue.Count > 0 && queue.Max() < cutoff)
                    {
                        _requestTimes.TryRemove(key, out _);
                    }
                }
            }
        }
    }
}
