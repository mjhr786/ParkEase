using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ParkingApp.API.Options;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.Options;
using CorporateSsoOptions = ParkingApp.Identity.Application.Options.CorporateSsoOptions;

namespace ParkingApp.API.Middleware;

/// <summary>
/// Simple in-process sliding-window rate limit per client IP.
/// Skips CORS preflight, health checks, hubs, and static SPA/asset paths so free-tier
/// page loads do not burn the API budget.
/// IoT LPR routes use a stricter per-IP budget from <see cref="IotLprOptions.RateLimitPerMinute"/>.
/// External auth routes use a stricter per-IP budget from <see cref="ExternalAuthOptions.RateLimitPerMinute"/>.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IConfiguration? _configuration;
    private readonly IOptionsMonitor<IotLprOptions>? _iotOptions;
    private readonly IOptionsMonitor<ExternalAuthOptions>? _externalAuthOptions;
    private readonly IOptionsMonitor<CorporateSsoOptions>? _corporateSsoOptions;
    private readonly IHostEnvironment? _env;
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _requestTimes = new();
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _iotRequestTimes = new();
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _externalAuthRequestTimes = new();
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> _corporateSsoRequestTimes = new();
    private static readonly Timer _cleanupTimer;
    private const int MaxRequests = 100;
    private const int DefaultIotMaxRequests = 30;
    private const int DefaultExternalAuthMaxRequests = 20;
    private const int DefaultCorporateSsoMaxRequests = 15;
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
        ILogger<RateLimitingMiddleware> logger)
        : this(next, logger, null, null, null, null, null)
    {
    }

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IOptionsMonitor<IotLprOptions>? iotOptions,
        IOptionsMonitor<ExternalAuthOptions>? externalAuthOptions,
        IOptionsMonitor<CorporateSsoOptions>? corporateSsoOptions)
        : this(next, logger, null, iotOptions, externalAuthOptions, corporateSsoOptions, null)
    {
    }

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration? configuration,
        IOptionsMonitor<IotLprOptions>? iotOptions,
        IOptionsMonitor<ExternalAuthOptions>? externalAuthOptions,
        IOptionsMonitor<CorporateSsoOptions>? corporateSsoOptions,
        IHostEnvironment? env)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
        _iotOptions = iotOptions;
        _externalAuthOptions = externalAuthOptions;
        _corporateSsoOptions = corporateSsoOptions;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_configuration?.GetValue<bool>("RateLimiting:Disabled", false) == true ||
            _configuration?.GetValue<bool>("RateLimiting:Enabled", true) == false ||
            _env?.IsEnvironment("Testing") == true)
        {
            await _next(context);
            return;
        }

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

        if (IsExternalAuthPath(context.Request.Path))
        {
            var externalLimit = ResolveExternalAuthLimitPerMinute();
            if (!IsRequestAllowed(clientId, _externalAuthRequestTimes, externalLimit))
            {
                _logger.LogWarning(
                    "External auth rate limit exceeded Client={ClientId} LimitPerMinute={Limit}",
                    clientId, externalLimit);
                context.Response.StatusCode = 429;
                context.Response.Headers.Append("Retry-After", WindowSeconds.ToString());
                await context.Response.WriteAsJsonAsync(new ApiResponse<object>(
                    false,
                    "Rate limit exceeded. Please try again later.",
                    null,
                    new List<string> { "rate_limited" },
                    "rate_limited"));
                return;
            }

            await _next(context);
            return;
        }

        // Corporate SSO-only bucket (skip global 100/min double-count) — KD-CS-17
        if (IsCorporateSsoPath(context.Request.Path))
        {
            var ssoLimit = ResolveCorporateSsoLimitPerMinute();
            if (!IsRequestAllowed(clientId, _corporateSsoRequestTimes, ssoLimit))
            {
                _logger.LogWarning(
                    "Corporate SSO rate limit exceeded Client={ClientId} LimitPerMinute={Limit}",
                    clientId, ssoLimit);
                context.Response.StatusCode = 429;
                context.Response.Headers.Append("Retry-After", WindowSeconds.ToString());
                await context.Response.WriteAsJsonAsync(new ApiResponse<object>(
                    false,
                    "Rate limit exceeded. Please try again later.",
                    null,
                    new List<string> { "rate_limited" },
                    "rate_limited"));
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

    private int ResolveExternalAuthLimitPerMinute()
    {
        var configured = _externalAuthOptions?.CurrentValue.RateLimitPerMinute ?? DefaultExternalAuthMaxRequests;
        return Math.Clamp(configured, 1, 10_000);
    }

    private int ResolveCorporateSsoLimitPerMinute()
    {
        var configured = _corporateSsoOptions?.CurrentValue.RateLimitPerMinute ?? DefaultCorporateSsoMaxRequests;
        return Math.Clamp(configured, 1, 10_000);
    }

    /// <summary>Exposed for unit tests.</summary>
    public static bool IsIotPath(PathString path) =>
        path.StartsWithSegments("/api/iot", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Stricter budget for token-exchange and related external auth mutations.
    /// GET providers is included (cheap but still per-IP).
    /// </summary>
    public static bool IsExternalAuthPath(PathString path) =>
        path.StartsWithSegments("/api/auth/external", StringComparison.OrdinalIgnoreCase);

    /// <summary>SSO-only rate limit bucket (does not double-count global budget).</summary>
    public static bool IsCorporateSsoPath(PathString path) =>
        path.StartsWithSegments("/api/auth/corporate/sso", StringComparison.OrdinalIgnoreCase);

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
        CleanupStore(_externalAuthRequestTimes, cutoff);
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
