namespace ParkingApp.Marketplace.Application.Options;

/// <summary>
/// Availability forecast knobs. Bound from configuration section <c>Forecast</c>.
/// Default: feature and ML both off (no prediction UI/API work).
/// </summary>
public sealed class ForecastOptions
{
    public const string SectionName = "Forecast";

    /// <summary>
    /// Master switch for availability forecasts (API + computation).
    /// When false, endpoints short-circuit and return empty/disabled results.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// When false (default), never call ML.NET. Requires <see cref="Enabled"/> to be true
    /// for any forecast work; even then only deterministic booking/history math runs.
    /// When true, optional ML.NET model may refine baseline rates (CPU/RAM heavy).
    /// </summary>
    public bool EnableMl { get; set; } = false;

    /// <summary>Upper bound for forecast horizon (hours). Clamped to 1–48.</summary>
    public int MaxHorizonHours { get; set; } = 24;

    /// <summary>
    /// Minimum allowed bucket interval in minutes (15, 30, or 60 after snap).
    /// Free tier should use 60 to keep bucket count low.
    /// </summary>
    public int MinIntervalMinutes { get; set; } = 60;

    /// <summary>Cache TTL for a single parking forecast.</summary>
    public int SingleCacheMinutes { get; set; } = 5;

    /// <summary>Cache TTL for owner multi-listing forecasts.</summary>
    public int OwnerCacheMinutes { get; set; } = 3;

    /// <summary>How long a trained ML model artifact is kept in memory (when ML enabled).</summary>
    public int ModelCacheHours { get; set; } = 12;
}
