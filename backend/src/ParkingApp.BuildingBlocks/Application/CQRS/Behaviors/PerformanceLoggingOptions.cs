namespace ParkingApp.Application.CQRS.Behaviors;

/// <summary>
/// Free-tier friendly CQRS logging knobs.
/// Bound from configuration section <c>Logging:Performance</c>.
/// </summary>
public sealed class PerformanceLoggingOptions
{
    public const string SectionName = "Logging:Performance";

    /// <summary>
    /// Successful commands/queries at or above this duration (ms) log at Information.
    /// Faster ones log at Debug only.
    /// </summary>
    public int SlowRequestMs { get; set; } = 200;
}
