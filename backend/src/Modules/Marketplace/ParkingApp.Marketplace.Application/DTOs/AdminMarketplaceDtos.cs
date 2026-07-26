using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Application.DTOs;

// ── Listings ──────────────────────────────────────────────────────────────

public sealed record AdminListingListItemDto(
    Guid Id,
    string Title,
    string City,
    string State,
    string Address,
    Guid OwnerId,
    bool IsActive,
    bool IsVerified,
    bool IsCorporateOnly,
    decimal HourlyRate,
    DateTime CreatedAt);

public sealed record AdminListingDetailDto(
    Guid Id,
    string Title,
    string Description,
    string City,
    string State,
    string Country,
    string Address,
    string PostalCode,
    string? ZoneCode,
    Guid OwnerId,
    Guid? CompanyOwnerId,
    bool IsActive,
    bool IsVerified,
    bool IsCorporateOnly,
    int TotalSpots,
    int AvailableSpots,
    decimal HourlyRate,
    decimal DailyRate,
    DateTime CreatedAt);

public sealed record AdminListingPageDto(
    IReadOnlyList<AdminListingListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record AdminListingReasonRequest(string Reason);

// ── Bookings ──────────────────────────────────────────────────────────────

public sealed record AdminBookingListItemDto(
    Guid Id,
    string? BookingReference,
    Guid UserId,
    Guid ParkingSpaceId,
    string? ParkingSpaceTitle,
    BookingStatus Status,
    DateTime StartDateTime,
    DateTime EndDateTime,
    decimal TotalAmount,
    string? VehicleNumber,
    DateTime CreatedAt);

public sealed record AdminBookingDetailDto(
    Guid Id,
    string? BookingReference,
    Guid UserId,
    Guid ParkingSpaceId,
    string? ParkingSpaceTitle,
    Guid? ParkingSpaceOwnerId,
    BookingStatus Status,
    DateTime StartDateTime,
    DateTime EndDateTime,
    decimal BaseAmount,
    decimal TaxAmount,
    decimal ServiceFee,
    decimal TotalAmount,
    string? VehicleNumber,
    string? CancellationReason,
    DateTime? CancelledAt,
    Guid? PaymentId,
    PaymentStatus? PaymentStatus,
    decimal? PaymentAmount,
    decimal? RefundAmount,
    bool HasPendingExtension,
    DateTime CreatedAt);

public sealed record AdminBookingPageDto(
    IReadOnlyList<AdminBookingListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record AdminCancelBookingRequest(string Reason);

// ── Payments ──────────────────────────────────────────────────────────────

public sealed record AdminPaymentListItemDto(
    Guid Id,
    Guid BookingId,
    Guid UserId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    PaymentMethod PaymentMethod,
    string? TransactionId,
    decimal? RefundAmount,
    DateTime? PaidAt,
    DateTime CreatedAt);

public sealed record AdminPaymentDetailDto(
    Guid Id,
    Guid BookingId,
    Guid UserId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    PaymentMethod PaymentMethod,
    string? TransactionId,
    string? PaymentGateway,
    string? InvoiceNumber,
    decimal? RefundAmount,
    string? RefundReason,
    string? RefundTransactionId,
    DateTime? PaidAt,
    DateTime? RefundedAt,
    DateTime CreatedAt,
    decimal RemainingRefundable);

public sealed record AdminPaymentPageDto(
    IReadOnlyList<AdminPaymentListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <param name="Amount">When null or ≤ 0, refunds the full remaining balance.</param>
public sealed record AdminRefundPaymentRequest(string Reason, decimal? Amount = null);
