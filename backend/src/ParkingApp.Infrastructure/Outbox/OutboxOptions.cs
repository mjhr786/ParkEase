namespace ParkingApp.Infrastructure.Outbox;

/// <summary>
/// Background outbox poll cadence. Bound from configuration section <c>Outbox</c>.
/// Defaults favor free-tier DB connection limits (Supabase pooler + Pooling=false).
/// </summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Base delay (seconds) when the last poll found no work. Clamped at runtime.</summary>
    public int PollIntervalSeconds { get; set; } = 15;

    /// <summary>Delay (seconds) after a poll that processed at least one message.</summary>
    public int BusyPollIntervalSeconds { get; set; } = 5;

    /// <summary>Maximum empty-queue backoff (seconds).</summary>
    public int EmptyBackoffMaxSeconds { get; set; } = 60;

    /// <summary>Max messages claimed per poll.</summary>
    public int BatchSize { get; set; } = 50;
}
