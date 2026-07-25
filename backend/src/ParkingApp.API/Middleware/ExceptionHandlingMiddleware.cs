using System.Net;
using System.Text.Json;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;

using ParkingApp.BuildingBlocks.Exceptions;

namespace ParkingApp.API.Middleware;

public class ExceptionHandlingMiddleware
{
    /// <summary>Nginx-style "Client Closed Request" — not a server fault.</summary>
    public const int StatusClientClosedRequest = 499;

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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected (e.g. abandoned /auth/refresh) — not a 500.
            // Set 499 so request logging does not report success (200) or server error.
            _logger.LogDebug("Request aborted by client: {Path}", context.Request.Path);
            if (!context.Response.HasStarted)
                context.Response.StatusCode = StatusClientClosedRequest;
        }
        catch (Exception ex)
        {
            if (IsClientFault(ex) || ex is DomainException)
                _logger.LogWarning(ex, "Client/domain fault: {Message}", ex.Message);
            else
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
            return;

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

    private static bool IsClientFault(Exception exception) =>
        exception is BadHttpRequestException
            or ArgumentException
            or UnauthorizedAccessException
            or KeyNotFoundException
            or InvalidOperationException;

    private static (HttpStatusCode StatusCode, string Message, List<string> Errors) MapException(Exception exception)
    {
        return exception switch
        {
            // Incomplete/truncated body, bad Content-Length, etc. — client fault, not a 500.
            BadHttpRequestException badRequest => (
                (HttpStatusCode)badRequest.StatusCode,
                "Invalid or incomplete request",
                new List<string> { badRequest.Message }),

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


