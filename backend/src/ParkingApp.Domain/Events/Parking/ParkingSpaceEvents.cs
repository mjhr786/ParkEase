using ParkingApp.Domain.Events;

namespace ParkingApp.Domain.Events.Parking;

public sealed record ParkingSpaceCreatedEvent(Guid ParkingSpaceId, Guid OwnerId, string Title) : DomainEvent;
public sealed record ParkingSpaceUpdatedEvent(Guid ParkingSpaceId, string Title) : DomainEvent;
public sealed record ParkingSpaceDeletedEvent(Guid ParkingSpaceId, Guid OwnerId) : DomainEvent;
public sealed record ParkingSpaceToggledEvent(Guid ParkingSpaceId, bool IsActive) : DomainEvent;
