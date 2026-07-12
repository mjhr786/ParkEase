namespace ParkingApp.Infrastructure.Caching;

/// <summary>
/// Production Redis / Upstash configuration.
/// Tuned for limited free-tier storage and command bandwidth.
/// </summary>
public sealed class RedisCacheOptions
{
    public const string SectionName = "Redis";

    /// <summary>Key prefix isolating this app/environment (e.g. ParkEase_Prod_).</summary>
    public string InstanceName { get; set; } = "ParkEase_";

    /// <summary>
    /// Applied when callers omit expiry so keys cannot live forever on limited storage.
    /// </summary>
    public int DefaultTtlMinutes { get; set; } = 15;

    /// <summary>Hard ceiling for any cache entry TTL.</summary>
    public int MaxTtlMinutes { get; set; } = 60;

    /// <summary>Compress JSON payloads at or above this size (bytes).</summary>
    public int CompressionThresholdBytes { get; set; } = 256;

    public int ConnectTimeoutMs { get; set; } = 5_000;

    public int SyncTimeoutMs { get; set; } = 5_000;

    public int AsyncTimeoutMs { get; set; } = 5_000;

    public int ConnectRetry { get; set; } = 3;

    /// <summary>TCP keepalive seconds (helps long-lived Upstash connections).</summary>
    public int KeepAliveSeconds { get; set; } = 60;

    /// <summary>
    /// Namespaces that use version-stamp invalidation instead of KEYS/SCAN.
    /// Pattern removes (e.g. search:*) become a single INCR — ideal for Upstash.
    /// Always read version from Redis (no process-local version cache) so multi-instance
    /// invalidation is immediately visible and users never see stale discovery data.
    /// </summary>
    public string[] VersionedNamespaces { get; set; } =
        ["search", "map", "parking-forecast", "owner-parking-forecast"];
}
