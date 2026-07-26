using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ParkingApp.API.Options;
using ParkingApp.Application.DTOs;

namespace ParkingApp.API.Middleware;

/// <summary>
/// Simple in-process sliding-window rate limit per client IP.
/// Skips CORS preflight, health checks, hubs, and static SPA/asset paths so free-tier
/// page loads do not burn the API budget.
/// IoT LPR routes use a stricter per-IP budget from <see cref="IotLprOptions.RateLimitPerMinute"/>.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IOptionsMonitor<IotLprOptions>? _iotOptions;
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _requestTimes = new();
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _iotRequestTimes = new();
    private static readonly Timer _cleanupTimer;
    private const int MaxRequests = 100;
    private const int DefaultIotMaxRequests = 30;
    private const int WindowSeconds = 60;
    private const int CleanupIntervalMinutes = 5;

    private static readonly string[] StaticExtensions =
    [
        ".js", ".css", ".map", ".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico",
        ".woff", ".woff2", ".ttf", ".eot", ".json", ".txt", ".br", ".gz"
    ];

    static RateLimitingMiddleware()
    {
        _cleanupTimer = new Timer(CleanupOldEntries, null,
            TimeSpan.FromMinutes(CleanupIntervalMinutes),
            TimeSpan.FromMinutes(CleanupIntervalMinutes));
    }

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IOptionsMonitor<IotLprOptions>? iotOptions = null)
    {
        _next = next;
        _logger = logger;
        _iotOptions = iotOptions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
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
        var isIot = IsIotPath(context.Request.Path);

        if (isIot)
        {
            var iotLimit = ResolveIotLimitPerMinute();
            if (!IsRequestAllowed(clientId, _iotRequestTimes, iotLimit))
            {
                _logger.LogWarning(
                    "IoT LPR rate limit exceeded Client={ClientId} LimitPerMinute={Limit}",
                    clientId, iotLimit);
                context.Response.StatusCode = 429;
                context.Response.Headers.Append("Retry-After", WindowSeconds.ToString());
                await context.Response.WriteAsJsonAsync(new ApiResponse<object>(
                    false, "IoT rate limit exceeded. Please try again later.", null));
                return;
            }

            await _next(context);
            return;
        }

        if (!IsRequestAllowed(clientId, _requestTimes, MaxRequests))
        {
            _logger.LogWarning("Rate limit exceeded for client: {ClientId}", clientId);
            context.Response.StatusCode = 429;
            context.Response.Headers.Append("Retry-After", WindowSeconds.ToString());
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>(
                false, "Rate limit exceeded. Please try again later.", null));
            return;
        }

        await _next(context);
    }

    private int ResolveIotLimitPerMinute()
    {
        var configured = _iotOptions?.CurrentValue.RateLimitPerMinute ?? DefaultIotMaxRequests;
        return Math.Clamp(configured, 1, 10_000);
    }

    /// <summary>Exposed for unit tests.</summary>
    public static bool IsIotPath(PathString path) =>
        path.StartsWithSegments("/api/iot", StringComparison.OrdinalIgnoreCase);

    /// <summary>Exposed for unit tests.</summary>
    public static bool ShouldSkipRateLimit(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.Length == 0 || value == "/")
            return true;

        if (path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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

    private static bool IsRequestAllowed(
        string clientId,
        ConcurrentDictionary<string, Queue<DateTime>> store,
        int maxRequests)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddSeconds(-WindowSeconds);

        var queue = store.GetOrAdd(clientId, _ => new Queue<DateTime>());

        lock (queue)
        {
            while (queue.Count > 0 && queue.Peek() < windowStart)
            {
                queue.Dequeue();
            }

            if (queue.Count >= maxRequests)
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
        var cutoff = now.AddMinutes(-10);

        CleanupStore(_requestTimes, cutoff);
        CleanupStore(_iotRequestTimes, cutoff);
    }

    private static void CleanupStore(ConcurrentDictionary<string, Queue<DateTime>> store, DateTime cutoff)
    {
        foreach (var key in store.Keys.ToList())
        {
            if (!store.TryGetValue(key, out var queue))
                continue;

            lock (queue)
            {
                if (queue.Count > 0 && queue.Max() < cutoff)
                    store.TryRemove(key, out _);
            }
        }
    }
}
