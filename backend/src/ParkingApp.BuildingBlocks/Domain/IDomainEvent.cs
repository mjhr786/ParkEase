namespace ParkingApp.BuildingBlocks.Domain;

/// <summary>
/// Marker interface for domain events. Shared kernel for modular monolith modules.
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
