namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Distributed cache service interface for Redis-based caching.
/// Provides high-performance caching for frequently accessed data.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a cached value by key.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets a value in cache with optional expiration.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a key exists in cache.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a specific key from cache.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes multiple keys matching a pattern (e.g., "parking:*").
    /// Use with caution as this can be expensive.
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Increments a counter (for rate limiting).
    /// </summary>
    Task<long> IncrementAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets or sets a value using a factory function if not cached.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
}
