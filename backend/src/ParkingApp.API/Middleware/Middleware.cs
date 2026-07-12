using System.Net;
using System.Text.Json;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;

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
            if (ex is DomainException)
                _logger.LogWarning(ex, "Domain exception: {Message}", ex.Message);
            else
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message, errors) = MapException(exception);

        context.Response.StatusCode = (int)statusCode;

        var response = new ApiResponse<object>(false, message, null, errors);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static (HttpStatusCode StatusCode, string Message, List<string> Errors) MapException(Exception exception)
    {
        return exception switch
        {
            NotFoundException notFound => (
                HttpStatusCode.NotFound,
                notFound.Message,
                new List<string> { notFound.Message }),

            ValidationException validation => (
                HttpStatusCode.BadRequest,
                validation.Message,
                validation.Errors.Count > 0
                    ? validation.Errors.SelectMany(kvp => kvp.Value).ToList()
                    : new List<string> { validation.Message }),

            UnauthorizedException unauthorized => (
                HttpStatusCode.Unauthorized,
                unauthorized.Message,
                new List<string> { unauthorized.Message }),

            ForbiddenException forbidden => (
                HttpStatusCode.Forbidden,
                forbidden.Message,
                new List<string> { forbidden.Message }),

            ConflictException conflict => (
                HttpStatusCode.Conflict,
                conflict.Message,
                new List<string> { conflict.Message }),

            BusinessRuleException businessRule => (
                HttpStatusCode.BadRequest,
                businessRule.Message,
                new List<string> { businessRule.Message }),

            DomainException domain => (
                HttpStatusCode.BadRequest,
                domain.Message,
                new List<string> { domain.Message }),

            ArgumentException argument => (
                HttpStatusCode.BadRequest,
                argument.Message,
                new List<string> { argument.Message }),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized access",
                new List<string> { "Unauthorized access" }),

            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                "Resource not found",
                new List<string> { "Resource not found" }),

            InvalidOperationException invalidOp => (
                HttpStatusCode.BadRequest,
                invalidOp.Message,
                new List<string> { invalidOp.Message }),

            _ => (
                HttpStatusCode.InternalServerError,
                "An error occurred. Please try again later.",
                new List<string> { "An error occurred. Please try again later." })
        };
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
        // CORS preflight must pass through without rate-limit short-circuiting.
        if (HttpMethods.IsOptions(context.Request.Method))
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
