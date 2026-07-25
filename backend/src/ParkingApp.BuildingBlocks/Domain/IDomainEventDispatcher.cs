namespace ParkingApp.BuildingBlocks.Domain;

/// <summary>
/// Dispatches domain events to registered <see cref="IDomainEventHandler{TEvent}"/> instances.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchEventsAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}
