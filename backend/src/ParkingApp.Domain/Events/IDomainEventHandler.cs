namespace ParkingApp.Domain.Events;

/// <summary>
/// Handler for a specific domain event type.
/// Implement this interface to react to domain events (e.g., send email, invalidate cache).
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
