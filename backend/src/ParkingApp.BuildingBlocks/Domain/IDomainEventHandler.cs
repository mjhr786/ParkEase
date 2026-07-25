namespace ParkingApp.BuildingBlocks.Domain;

/// <summary>
/// Handler for a specific domain event type. Shared kernel so module Application
/// assemblies can register handlers without referencing host Domain.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
