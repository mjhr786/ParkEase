namespace ParkingApp.Domain.Events;

/// <summary>
/// Marker interface for domain events. Raised by aggregate roots
/// and dispatched after the unit of work commits.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

/// <summary>
/// Base record for domain events. Use records for immutability.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
