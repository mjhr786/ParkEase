using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using StackExchange.Redis;

namespace ParkingApp.Infrastructure.Services;

/// <summary>
/// Redis-based distributed cache implementation.
/// Thread-safe and optimized for high-throughput scenarios.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly string _instancePrefix;
    private static readonly JsonSerializerOptions _jsonOptions = new() 
    { 
        PropertyNameCaseInsensitive = true 
    };

    public RedisCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger,
        string instancePrefix = "ParkingApp_")
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _logger = logger;
        _instancePrefix = instancePrefix;
    }

    private string GetKey(string key) => $"{_instancePrefix}{key}";

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetKey(key);
            var value = await _database.StringGetAsync(redisKey);
            
            if (!value.HasValue)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetKey(key);
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            
            if (expiry.HasValue)
            {
                await _database.StringSetAsync(redisKey, json);
                await _database.KeyExpireAsync(redisKey, expiry.Value);
            }
            else
            {
                await _database.StringSetAsync(redisKey, json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetKey(key);
            return await _database.KeyExistsAsync(redisKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache key exists: {Key}", key);
            return false;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetKey(key);
            await _database.KeyDeleteAsync(redisKey);
            _logger.LogDebug("Cache invalidated: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key: {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisPattern = GetKey(pattern);
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            await foreach (var key in server.KeysAsync(pattern: redisPattern))
            {
                await _database.KeyDeleteAsync(key);
            }
            
            _logger.LogDebug("Cache invalidated by pattern: {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache by pattern: {Pattern}", pattern);
        }
    }

    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = GetKey(key);
            var value = await _database.StringIncrementAsync(redisKey);
            
            if (expiry.HasValue && value == 1)
            {
                await _database.KeyExpireAsync(redisKey, expiry.Value);
            }
            
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing cache key: {Key}", key);
            return 0;
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        // Cache miss - fetch from source
        var value = await factory();
        
        // Store in cache
        if (value != null)
        {
            await SetAsync(key, value, expiry, cancellationToken);
        }

        return value;
    }
}
