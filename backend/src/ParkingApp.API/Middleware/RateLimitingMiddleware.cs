using System.Collections.Concurrent;
using ParkingApp.Application.DTOs;

namespace ParkingApp.API.Middleware;

/// <summary>
/// Simple in-process sliding-window rate limit per client IP.
/// Skips CORS preflight, health checks, hubs, and static SPA/asset paths so free-tier
/// page loads do not burn the API budget.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _requestTimes = new();
    private static readonly Timer _cleanupTimer;
    private const int MaxRequests = 100;
    private const int WindowSeconds = 60;
    private const int CleanupIntervalMinutes = 5;

    private static readonly string[] StaticExtensions =
    [
        ".js", ".css", ".map", ".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico",
        ".woff", ".woff2", ".ttf", ".eot", ".json", ".txt", ".br", ".gz"
    ];

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
        // CORS preflight must pass through without rate-limit short-circuiting.
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (ShouldSkipRateLimit(context.Request.Path))
        {
            await _next(context);
            return;
        }

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

    /// <summary>Exposed for unit tests.</summary>
    public static bool ShouldSkipRateLimit(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.Length == 0 || value == "/")
            return true;

        // Health + SignalR (long-lived / chatty) should not share the API REST budget.
        if (path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // SPA static assets and local uploads (when not using R2 public URLs).
        if (path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("/index.html", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/vite.svg", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(value);
        if (!string.IsNullOrEmpty(ext)
            && StaticExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
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
