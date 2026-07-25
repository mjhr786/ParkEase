namespace ParkingApp.Application.Contracts.IntegrationEvents;

/// <summary>
/// Stable cross-module integration event contracts (IDs and value data only ΓÇö no Domain entities).
/// Domain events may map to these when crossing module or process boundaries.
/// </summary>
public sealed record BookingCreatedIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    DateTime OccurredOnUtc);

public sealed record BookingApprovedIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    bool RequiresPayment,
    DateTime OccurredOnUtc);

public sealed record BookingRejectedIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    string? Reason,
    DateTime OccurredOnUtc);

public sealed record BookingCancelledIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    string? Reason,
    DateTime OccurredOnUtc);

public sealed record BookingConfirmedIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    DateTime OccurredOnUtc);

public sealed record BookingCheckedInIntegrationEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    DateTime OccurredOnUtc);

public sealed record PaymentCompletedIntegrationEvent(
    Guid PaymentId,
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    decimal Amount,
    string Currency,
    DateTime OccurredOnUtc);

public sealed record CorporateBookingRequestedIntegrationEvent(
    Guid CompanyId,
    Guid CorporateBookingId,
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    DateTime OccurredOnUtc);

public sealed record CorporateBookingConfirmedIntegrationEvent(
    Guid CompanyId,
    Guid CorporateBookingId,
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    DateTime OccurredOnUtc);

public sealed record CorporateWaitlistPromotedIntegrationEvent(
    Guid CompanyId,
    Guid WaitlistEntryId,
    Guid BookingId,
    Guid UserId,
    DateTime OccurredOnUtc);

public sealed record UserRegisteredIntegrationEvent(
    Guid UserId,
    string Email,
    DateTime OccurredOnUtc);
