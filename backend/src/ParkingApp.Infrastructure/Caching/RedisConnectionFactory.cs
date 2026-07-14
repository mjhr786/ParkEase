using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ParkingApp.Infrastructure.Caching;

/// <summary>
/// Builds StackExchange.Redis options suitable for Upstash (TLS) and local Redis Docker.
/// </summary>
public static class RedisConnectionFactory
{
    /// <summary>
    /// Returns true when a real Redis endpoint is configured (not empty / placeholder).
    /// Bare <c>localhost:6379</c> without a password is treated as unconfigured so local
    /// Docker must use an explicit connection (e.g. <c>localhost:6379,password=...</c>).
    /// </summary>
    public static bool IsConfigured(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        var value = connectionString.Trim();
        return value is not (
            "localhost:6379"
            or "127.0.0.1:6379"
            or "SET_VIA_USER_SECRETS_OR_ENV_VAR");
    }

    /// <summary>
    /// Human-readable cache target for startup logs (no secrets).
    /// </summary>
    public static string DescribeTarget(string? connectionString)
    {
        if (!IsConfigured(connectionString))
            return "not configured";

        var value = connectionString!.Trim();
        if (value.Contains("upstash.io", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
            return "Upstash";

        if (value.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || value.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            return "local Docker";

        return "Redis";
    }

    public static ConfigurationOptions CreateOptions(string connectionString, RedisCacheOptions cacheOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(cacheOptions);

        var options = ParseConnectionString(connectionString.Trim());

        // Upstash requires TLS; local Docker Redis uses plain TCP on 6379.
        if (IsTlsRequired(connectionString))
        {
            options.Ssl = true;
            options.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        }
        else
        {
            options.Ssl = false;
        }

        // Do not crash the host if Redis is briefly unavailable — degrade via cache service try/catch.
        options.AbortOnConnectFail = false;
        options.ConnectRetry = Math.Max(0, cacheOptions.ConnectRetry);
        options.ConnectTimeout = Math.Max(1_000, cacheOptions.ConnectTimeoutMs);
        options.SyncTimeout = Math.Max(1_000, cacheOptions.SyncTimeoutMs);
        options.AsyncTimeout = Math.Max(1_000, cacheOptions.AsyncTimeoutMs);
        options.KeepAlive = Math.Max(15, cacheOptions.KeepAliveSeconds);

        // Prefer fewer round-trips; Upstash bills commands + bandwidth.
        options.DefaultDatabase = 0;

        return options;
    }

    public static bool IsTlsRequired(string connectionString)
    {
        return connectionString.Contains("upstash.io", StringComparison.OrdinalIgnoreCase)
            || connectionString.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses classic SE.Redis CSV strings and redis(s):// URLs (Upstash REST/CLI format).
    /// <see cref="ConfigurationOptions.Parse"/> does not reliably handle rediss:// URIs.
    /// </summary>
    public static ConfigurationOptions ParseConnectionString(string connectionString)
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
            && (uri.Scheme.Equals("redis", StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)))
        {
            var options = new ConfigurationOptions
            {
                Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)
            };

            var port = uri.IsDefaultPort ? 6379 : uri.Port;
            options.EndPoints.Add(uri.Host, port);

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var userInfo = uri.UserInfo.Split(':', 2);
                if (userInfo.Length == 2)
                {
                    if (!string.IsNullOrEmpty(userInfo[0]))
                        options.User = Uri.UnescapeDataString(userInfo[0]);
                    options.Password = Uri.UnescapeDataString(userInfo[1]);
                }
                else
                {
                    options.Password = Uri.UnescapeDataString(userInfo[0]);
                }
            }

            return options;
        }

        // host:port,password=...,ssl=True style
        return ConfigurationOptions.Parse(connectionString);
    }

    public static IConnectionMultiplexer Connect(
        string connectionString,
        RedisCacheOptions cacheOptions,
        ILogger? logger = null)
    {
        var options = CreateOptions(connectionString, cacheOptions);
        logger?.LogInformation(
            "Connecting to Redis (ssl={Ssl}, endpoints={Endpoints}, instance={Instance})",
            options.Ssl,
            string.Join(',', options.EndPoints.Select(e => e.ToString())),
            cacheOptions.InstanceName);

        // Never log password / full connection string.
        var multiplexer = ConnectionMultiplexer.Connect(options);

        multiplexer.ConnectionFailed += (_, e) =>
            logger?.LogWarning(e.Exception, "Redis connection failed: {FailureType} {EndPoint}", e.FailureType, e.EndPoint);

        multiplexer.ConnectionRestored += (_, e) =>
            logger?.LogInformation("Redis connection restored: {EndPoint}", e.EndPoint);

        multiplexer.ErrorMessage += (_, e) =>
            logger?.LogWarning("Redis error message: {Message}", e.Message);

        return multiplexer;
    }
}
