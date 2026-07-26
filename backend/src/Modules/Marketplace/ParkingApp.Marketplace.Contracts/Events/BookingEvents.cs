using ParkingApp.BuildingBlocks.Domain;
namespace ParkingApp.Marketplace.Domain.Events;

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

/// <summary>Vendor rejected a pending booking request.</summary>
public sealed record BookingRejectedEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    string? Reason,
    Guid? VendorUserId) : DomainEvent;

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

/// <summary>Overstay fee assessed or increased on a booking.</summary>
public sealed record BookingOverstayFeeAssessedEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    decimal FeeAmount,
    int BillableMinutes,
    decimal DeltaAmount) : DomainEvent;

/// <summary>Overstay fee (or portion) paid successfully.</summary>
public sealed record BookingOverstayFeePaidEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    decimal PaidAmount,
    decimal RemainingOutstanding,
    string? TransactionId) : DomainEvent;

/// <summary>Member requested a booking end-time extension (PendingExtension).</summary>
public sealed record BookingExtensionRequestedEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    DateTime NewEndUtc,
    decimal ExtraAmount) : DomainEvent;

/// <summary>Vendor approved an extension; member may still need to pay.</summary>
public sealed record BookingExtensionApprovedEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    bool RequiresPayment,
    decimal ExtraAmount,
    DateTime? NewEndUtc,
    Guid? VendorUserId) : DomainEvent;

/// <summary>Vendor rejected an extension request.</summary>
public sealed record BookingExtensionRejectedEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    string? Reason,
    Guid? VendorUserId) : DomainEvent;

/// <summary>Extension applied to the booking (no further payment pending).</summary>
public sealed record BookingExtensionConfirmedEvent(
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    DateTime NewEndUtc,
    decimal ExtraAmount) : DomainEvent;

/// <summary>Payment completed for a booking or extension.</summary>
public sealed record PaymentCompletedEvent(
    Guid PaymentId,
    Guid BookingId,
    Guid UserId,
    Guid ParkingSpaceId,
    string? BookingReference,
    decimal Amount,
    string Currency,
    bool IsExtensionPayment) : DomainEvent;
