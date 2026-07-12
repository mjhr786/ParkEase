namespace ParkingApp.Domain.Events.Bookings;

/// <summary>Marketplace booking request created (Pending).</summary>
public sealed record BookingRequestedEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference) : DomainEvent;

/// <summary>Vendor approved a pending booking; member may still need to pay.</summary>
public sealed record BookingApprovedEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    bool RequiresPayment) : DomainEvent;

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
