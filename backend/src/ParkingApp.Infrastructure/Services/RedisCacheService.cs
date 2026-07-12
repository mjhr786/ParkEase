using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Interfaces;
using ParkingApp.Infrastructure.Caching;
using StackExchange.Redis;

namespace ParkingApp.Infrastructure.Services;

/// <summary>
/// Production Redis cache tuned for Upstash free-tier limits (storage + bandwidth).
/// <list type="bullet">
/// <item>Single-round-trip SET with expiry (no SET+EXPIRE)</item>
/// <item>Default/max TTL so keys cannot grow without bound</item>
/// <item>GZip for larger JSON payloads</item>
/// <item>Version-stamp invalidation for search/map (1 INCR vs KEYS/SCAN)</item>
/// <item>Safe distributed locks (unique token + Lua release)</item>
/// <item>Fail-open on Redis errors so the API stays available</item>
/// </list>
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    /// <summary>Payload framing: '0' = UTF-8 JSON, '1' = GZip(UTF-8 JSON).</summary>
    private const byte PayloadPlain = (byte)'0';
    private const byte PayloadGzip = (byte)'1';

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Releases a lock only if the caller still owns it (compare-and-delete).
    /// </summary>
    private static readonly LuaScript ReleaseLockLua = LuaScript.Prepare(
        """
        if redis.call('get', @key) == @token then
          return redis.call('del', @key)
        else
          return 0
        end
        """);

    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly RedisCacheOptions _options;
    private readonly string _instancePrefix;
    private readonly TimeSpan _defaultTtl;
    private readonly TimeSpan _maxTtl;
    private readonly HashSet<string> _versionedNamespaces;

    /// <summary>In-process stampede locks for GetOrSet (per logical key).</summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _getOrSetGates = new();

    /// <summary>Per-instance lock ownership tokens (physical redis key → token).</summary>
    private readonly ConcurrentDictionary<string, string> _lockTokens = new();

    public RedisCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger,
        IOptions<RedisCacheOptions>? options = null)
        : this(redis, logger, options?.Value ?? new RedisCacheOptions())
    {
    }

    /// <summary>Test-friendly constructor with explicit instance prefix.</summary>
    public RedisCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger,
        string instancePrefix)
        : this(redis, logger, new RedisCacheOptions { InstanceName = instancePrefix })
    {
    }

    public RedisCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger,
        RedisCacheOptions options)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new RedisCacheOptions();
        _database = redis.GetDatabase();
        _instancePrefix = string.IsNullOrWhiteSpace(_options.InstanceName)
            ? "ParkEase_"
            : _options.InstanceName;
        _defaultTtl = TimeSpan.FromMinutes(Math.Clamp(_options.DefaultTtlMinutes, 1, 24 * 60));
        _maxTtl = TimeSpan.FromMinutes(Math.Clamp(_options.MaxTtlMinutes, 1, 24 * 60));
        _versionedNamespaces = new HashSet<string>(
            _options.VersionedNamespaces ?? ["search", "map", "parking-forecast", "owner-parking-forecast"],
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = await ResolvePhysicalKeyAsync(key).ConfigureAwait(false);
            var value = await _database.StringGetAsync(redisKey).ConfigureAwait(false);
            if (!value.HasValue)
                return default;

            return Deserialize<T>((byte[])value!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = await ResolvePhysicalKeyAsync(key).ConfigureAwait(false);
            var payload = Serialize(value);
            var ttl = NormalizeTtl(expiry);

            // Single round-trip: SET key value EX seconds
            await _database.StringSetAsync(redisKey, payload, ttl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = await ResolvePhysicalKeyAsync(key).ConfigureAwait(false);
            return await _database.KeyExistsAsync(redisKey).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis EXISTS failed for key {Key}", key);
            return false;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = await ResolvePhysicalKeyAsync(key).ConfigureAwait(false);
            await _database.KeyDeleteAsync(redisKey).ConfigureAwait(false);
            _logger.LogDebug("Cache removed: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis DEL failed for key {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            // Prefer O(1) version bump for known high-churn namespaces (search:*, map:*).
            if (TryGetNamespaceFromPattern(pattern, out var ns) && _versionedNamespaces.Contains(ns))
            {
                var versionKey = VersionKey(ns);
                await _database.StringIncrementAsync(versionKey).ConfigureAwait(false);
                // Version keys themselves need a long TTL so they are not lost, but not infinite.
                await _database.KeyExpireAsync(versionKey, TimeSpan.FromDays(30)).ConfigureAwait(false);
                _logger.LogDebug("Cache namespace version bumped: {Namespace} (pattern {Pattern})", ns, pattern);
                return;
            }

            // Fallback: SCAN + batched DEL (never KEYS — expensive on Upstash).
            await ScanAndDeleteAsync(pattern, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Cache invalidated by SCAN pattern: {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis pattern invalidation failed for {Pattern}", pattern);
        }
    }

    public async Task<long> IncrementAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = await ResolvePhysicalKeyAsync(key).ConfigureAwait(false);
            var value = await _database.StringIncrementAsync(redisKey).ConfigureAwait(false);

            // Only attach TTL on first increment to avoid extending windows unintentionally.
            if (value == 1 && expiry.HasValue)
                await _database.KeyExpireAsync(redisKey, NormalizeTtl(expiry)).ConfigureAwait(false);

            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis INCR failed for key {Key}", key);
            return 0;
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
            return cached;

        // Single-flight per key in this process to limit stampede traffic to Redis + DB.
        var gate = _getOrSetGates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = await GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
                return cached;

            var value = await factory().ConfigureAwait(false);
            if (value is not null)
                await SetAsync(key, value, expiry, cancellationToken).ConfigureAwait(false);

            return value;
        }
        finally
        {
            gate.Release();
            // Bound gate dictionary growth for one-off keys.
            if (gate.CurrentCount == 1)
                _getOrSetGates.TryRemove(key, out _);
        }
    }

    public async Task<bool> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = LockKey(key);
            var token = Guid.NewGuid().ToString("N");
            // SET key token NX EX seconds — atomic, single round-trip.
            // Explicit flags overload keeps Moq/test surfaces stable across SE.Redis versions.
            var acquired = await _database.StringSetAsync(
                redisKey,
                token,
                NormalizeTtl(expiry),
                When.NotExists,
                CommandFlags.None).ConfigureAwait(false);

            if (acquired)
                _lockTokens[redisKey] = token;

            return acquired;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis lock acquire failed for key {Key}", key);
            return false;
        }
    }

    public async Task ReleaseLockAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var redisKey = LockKey(key);
            if (!_lockTokens.TryRemove(redisKey, out var token))
            {
                // Token lost (process recycle) — do not DEL blindly (would steal another owner's lock).
                return;
            }

            await _database.ScriptEvaluateAsync(
                ReleaseLockLua,
                new { key = (RedisKey)redisKey, token = (RedisValue)token }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis lock release failed for key {Key}", key);
        }
    }

    // ── Key resolution (versioned namespaces) ───────────────────────────────

    private async Task<RedisKey> ResolvePhysicalKeyAsync(string logicalKey)
    {
        if (TrySplitNamespace(logicalKey, out var ns, out var remainder)
            && _versionedNamespaces.Contains(ns))
        {
            var version = await GetNamespaceVersionAsync(ns).ConfigureAwait(false);
            return $"{_instancePrefix}{ns}:v{version}:{remainder}";
        }

        return $"{_instancePrefix}{logicalKey}";
    }

    private string VersionKey(string ns) => $"{_instancePrefix}ver:{ns}";

    private string LockKey(string key) => $"{_instancePrefix}lock:{key}";

    /// <summary>
    /// Always read version from Redis — never process-local cache.
    /// Local caching caused up to multi-second stale search/map after another instance invalidated.
    /// </summary>
    private async Task<long> GetNamespaceVersionAsync(string ns)
    {
        var raw = await _database.StringGetAsync(VersionKey(ns)).ConfigureAwait(false);
        return raw.HasValue && long.TryParse((string?)raw, out var parsed) ? parsed : 0;
    }

    private static bool TrySplitNamespace(string logicalKey, out string ns, out string remainder)
    {
        ns = string.Empty;
        remainder = string.Empty;
        if (string.IsNullOrEmpty(logicalKey))
            return false;

        var idx = logicalKey.IndexOf(':');
        if (idx <= 0)
            return false;

        ns = logicalKey[..idx];
        remainder = logicalKey[(idx + 1)..];
        return true;
    }

    private static bool TryGetNamespaceFromPattern(string pattern, out string ns)
    {
        ns = string.Empty;
        // Expect "search:*" or "map:*"
        if (string.IsNullOrEmpty(pattern))
            return false;

        var star = pattern.IndexOf('*');
        if (star <= 0)
            return false;

        var beforeStar = pattern[..star].TrimEnd(':');
        if (beforeStar.Contains(':') || beforeStar.Length == 0)
            return false;

        ns = beforeStar;
        return true;
    }

    // ── SCAN fallback (rare paths only) ─────────────────────────────────────

    private async Task ScanAndDeleteAsync(string pattern, CancellationToken cancellationToken)
    {
        var redisPattern = $"{_instancePrefix}{pattern}";
        var endpoints = _redis.GetEndPoints();
        if (endpoints.Length == 0)
            return;

        // Upstash is single-endpoint; still iterate safely.
        foreach (var endpoint in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IServer server;
            try
            {
                server = _redis.GetServer(endpoint);
            }
            catch
            {
                continue;
            }

            if (server is null || !server.IsConnected || server.IsReplica)
                continue;

            var batch = new List<RedisKey>(64);
            await foreach (var key in server.KeysAsync(pattern: redisPattern, pageSize: 50)
                               .WithCancellation(cancellationToken)
                               .ConfigureAwait(false))
            {
                batch.Add(key);
                if (batch.Count >= 64)
                {
                    await _database.KeyDeleteAsync(batch.ToArray()).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
                await _database.KeyDeleteAsync(batch.ToArray()).ConfigureAwait(false);
        }
    }

    // ── Serialization / compression ─────────────────────────────────────────

    private TimeSpan NormalizeTtl(TimeSpan? expiry)
    {
        var ttl = expiry ?? _defaultTtl;
        if (ttl <= TimeSpan.Zero)
            ttl = _defaultTtl;
        if (ttl > _maxTtl)
            ttl = _maxTtl;
        return ttl;
    }

    private byte[] Serialize<T>(T value)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        var threshold = Math.Max(64, _options.CompressionThresholdBytes);

        if (json.Length < threshold)
            return Prefix(PayloadPlain, json);

        var compressed = Gzip(json);
        // Only keep compression if it actually saves bytes (header + payload).
        if (compressed.Length + 1 < json.Length)
            return Prefix(PayloadGzip, compressed);

        return Prefix(PayloadPlain, json);
    }

    private static T? Deserialize<T>(byte[] payload)
    {
        if (payload is null || payload.Length == 0)
            return default;

        // Framed GZip (marker + compressed body). GZip streams are never tiny;
        // raw INCR counters like "1" must not be treated as GZip.
        if (payload[0] == PayloadGzip && payload.Length > 12)
        {
            try
            {
                var json = Gunzip(payload.AsSpan(1).ToArray());
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (InvalidDataException)
            {
                // Fall through — treat as unframed.
            }
            catch (Exception)
            {
                // Fall through — treat as unframed.
            }
        }

        // Framed plain JSON (marker + UTF-8 JSON body).
        if (payload[0] == PayloadPlain && payload.Length > 1)
        {
            var body = payload.AsSpan(1);
            // Framed payloads after our serializer always start with JSON tokens.
            if (body[0] is (byte)'{' or (byte)'[' or (byte)'"' or (byte)'t' or (byte)'f' or (byte)'n'
                || body[0] is >= (byte)'0' and <= (byte)'9')
            {
                return JsonSerializer.Deserialize<T>(body, JsonOptions);
            }
        }

        // Unframed / legacy JSON, or raw Redis values (e.g. INCR counters).
        return JsonSerializer.Deserialize<T>(payload, JsonOptions);
    }

    private static byte[] Prefix(byte kind, byte[] body)
    {
        var result = new byte[body.Length + 1];
        result[0] = kind;
        Buffer.BlockCopy(body, 0, result, 1, body.Length);
        return result;
    }

    private static byte[] Gzip(byte[] data)
    {
        using var output = new MemoryStream(data.Length / 2);
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(data, 0, data.Length);
        return output.ToArray();
    }

    private static byte[] Gunzip(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
