using Microsoft.Extensions.Logging;

namespace ParkingApp.BuildingBlocks.Logging;

/// <summary>
/// Extension methods for structured logging with consistent formatting
/// </summary>
public static class LoggingExtensions
{
    // Performance logging
    public static void LogOperationStart(this ILogger logger, string operation, params object[] args)
    {
        logger.LogInformation("Starting {Operation} " + string.Join(" ", Enumerable.Range(0, args.Length).Select(i => $"{{{i}}}")), 
            new object[] { operation }.Concat(args).ToArray());
    }
    
    public static void LogOperationComplete(this ILogger logger, string operation, TimeSpan duration)
    {
        logger.LogInformation("Completed {Operation} in {Duration:0.00}ms", operation, duration.TotalMilliseconds);
    }
    
    public static void LogOperationFailed(this ILogger logger, string operation, Exception exception)
    {
        logger.LogError(exception, "Failed {Operation}", operation);
    }
    
    // Entity operations
    public static void LogEntityCreated<T>(this ILogger logger, object id)
    {
        logger.LogInformation("{EntityType} created with Id: {EntityId}", typeof(T).Name, id);
    }
    
    public static void LogEntityUpdated<T>(this ILogger logger, object id)
    {
        logger.LogInformation("{EntityType} updated: {EntityId}", typeof(T).Name, id);
    }
    
    public static void LogEntityDeleted<T>(this ILogger logger, object id)
    {
        logger.LogWarning("{EntityType} deleted: {EntityId}", typeof(T).Name, id);
    }
    
    public static void LogEntityNotFound<T>(this ILogger logger, object id)
    {
        logger.LogWarning("{EntityType} not found: {EntityId}", typeof(T).Name, id);
    }
    
    // Cache operations
    public static void LogCacheHit(this ILogger logger, string cacheKey)
    {
        logger.LogDebug("Cache HIT: {CacheKey}", cacheKey);
    }
    
    public static void LogCacheMiss(this ILogger logger, string cacheKey)
    {
        logger.LogDebug("Cache MISS: {CacheKey}", cacheKey);
    }
    
    public static void LogCacheInvalidated(this ILogger logger, string pattern)
    {
        logger.LogDebug("Cache invalidated: {CachePattern}", pattern);
    }
    
    // Authentication/Authorization
    public static void LogUserAuthenticated(this ILogger logger, object userId, string email)
    {
        logger.LogInformation("User authenticated: {UserId} ({Email})", userId, email);
    }
    
    public static void LogAuthenticationFailed(this ILogger logger, string email, string reason)
    {
        logger.LogWarning("Authentication failed for {Email}: {Reason}", email, reason);
    }
    
    public static void LogUnauthorizedAccess(this ILogger logger, object userId, string resource)
    {
        logger.LogWarning("Unauthorized access attempt by {UserId} to {Resource}", userId, resource);
    }
    
    // Payment operations
    public static void LogPaymentInitiated(this ILogger logger, object paymentId, decimal amount)
    {
        logger.LogInformation("Payment {PaymentId} initiated for amount {Amount:C}", paymentId, amount);
    }
    
    public static void LogPaymentCompleted(this ILogger logger, object paymentId, string transactionId)
    {
        logger.LogInformation("Payment {PaymentId} completed, TransactionId: {TransactionId}", paymentId, transactionId);
    }
    
    public static void LogPaymentFailed(this ILogger logger, object paymentId, string reason)
    {
        logger.LogError("Payment {PaymentId} failed: {Reason}", paymentId, reason);
    }
    
    // External service calls
    public static void LogExternalServiceCall(this ILogger logger, string serviceName, string operation)
    {
        logger.LogDebug("Calling external service {ServiceName}: {Operation}", serviceName, operation);
    }
    
    public static void LogExternalServiceError(this ILogger logger, string serviceName, Exception exception)
    {
        logger.LogError(exception, "External service error from {ServiceName}", serviceName);
    }
}
