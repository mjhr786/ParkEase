namespace ParkingApp.Domain.Events.Bookings;

public sealed record BookingConfirmedEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference) : DomainEvent;

public sealed record BookingCancelledEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    string? Reason) : DomainEvent;

public sealed record BookingCheckedInEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference) : DomainEvent;

public sealed record BookingCheckedOutEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference) : DomainEvent;
