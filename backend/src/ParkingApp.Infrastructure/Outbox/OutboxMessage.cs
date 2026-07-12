namespace ParkingApp.Infrastructure.Outbox;

public enum OutboxStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3
}

/// <summary>
/// Persistent outbox row — stored in the same database transaction as domain writes.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>CLR type name (AssemblyQualifiedName preferred).</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>JSON payload of the domain event.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Unique key to prevent duplicate side effects under retry
    /// (e.g. BookingCancelledEvent:{BookingId}).
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? AvailableAfterUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }
}
