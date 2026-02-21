namespace ParkingApp.Domain.Events;

/// <summary>
/// Dispatches domain events to their registered handlers.
/// Called by the UnitOfWork after SaveChangesAsync.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchEventsAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
