using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Services;

/// <summary>
/// In-memory implementation of ICacheService for development and single-instance deployments.
/// Uses IMemoryCache internally.
/// </summary>
public class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new(); // To track keys for pattern matching (simulated)

    public InMemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions();
        if (expiry.HasValue)
        {
            options.SetAbsoluteExpiration(expiry.Value);
        }
        
        _cache.Set(key, value, options);
        _keys.TryAdd(key, 0); // Track key
        
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Simple pattern matching: starts with, ends with, or contains
        // Note: This is an approximation for Redis pattern matching
        var searchPattern = pattern.Replace("*", "");
        var keysToRemove = new List<string>();

        foreach (var key in _keys.Keys)
        {
            bool match = false;
            if (pattern.StartsWith("*") && pattern.EndsWith("*")) match = key.Contains(searchPattern);
            else if (pattern.StartsWith("*")) match = key.EndsWith(searchPattern);
            else if (pattern.EndsWith("*")) match = key.StartsWith(searchPattern);
            else match = key == pattern;

            if (match)
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            RemoveAsync(key);
        }

        return Task.CompletedTask;
    }

    public Task<long> IncrementAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        // Atomic increment simulation
        // Note: This is not perfectly thread-safe for high concurrency compared to Redis INCR
        // but sufficient for basic rate limiting in single instance.
        
        lock (_keys) // Simple lock for safety
        {
            long currentValue = 0;
            if (_cache.TryGetValue(key, out long val))
            {
                currentValue = val;
            }
            
            currentValue++;
            
            var options = new MemoryCacheEntryOptions();
            if (expiry.HasValue)
            {
                options.SetAbsoluteExpiration(expiry.Value);
            }
            
            _cache.Set(key, currentValue, options);
            _keys.TryAdd(key, 0);
            
            return Task.FromResult(currentValue);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out T? value) && value != null)
        {
            return value;
        }

        value = await factory();
        await SetAsync(key, value, expiry, cancellationToken);
        
        return value;
    }
}
