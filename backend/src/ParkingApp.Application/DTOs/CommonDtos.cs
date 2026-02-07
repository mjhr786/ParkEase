using System.ComponentModel.DataAnnotations;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.DTOs;

public record PaymentDto(
    Guid Id,
    Guid BookingId,
    Guid UserId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod,
    PaymentStatus Status,
    string? TransactionId,
    DateTime? PaidAt,
    string? ReceiptUrl,
    string? InvoiceNumber,
    DateTime CreatedAt
);

public record CreatePaymentDto(
    [Required] Guid BookingId,
    [Required] PaymentMethod PaymentMethod
);

public record PaymentResultDto(
    bool Success,
    string? TransactionId,
    PaymentStatus Status,
    string? Message,
    string? ReceiptUrl
);

public record RefundRequestDto(
    [Required] Guid PaymentId,
    [Required] decimal Amount,
    [Required] string Reason
);

public record RefundResultDto(
    bool Success,
    string? RefundTransactionId,
    decimal RefundedAmount,
    string? Message
);

// Review DTOs
public record ReviewDto(
    Guid Id,
    Guid UserId,
    string UserName,
    Guid ParkingSpaceId,
    Guid? BookingId,
    int Rating,
    string? Title,
    string? Comment,
    int HelpfulCount,
    string? OwnerResponse,
    DateTime? OwnerResponseAt,
    DateTime CreatedAt
);

public record CreateReviewDto(
    [Required] Guid ParkingSpaceId,
    Guid? BookingId,
    [Required][Range(1, 5)] int Rating,
    string? Title,
    string? Comment
);

public record UpdateReviewDto(
    [Range(1, 5)] int? Rating,
    string? Title,
    string? Comment
);

public record OwnerResponseDto(
    [Required] string Response
);

// Dashboard DTOs
public record VendorDashboardDto(
    int TotalParkingSpaces,
    int ActiveParkingSpaces,
    int TotalBookings,
    int ActiveBookings,
    int PendingBookings,
    int CompletedBookings,
    decimal TotalEarnings,
    decimal MonthlyEarnings,
    decimal WeeklyEarnings,
    double AverageRating,
    int TotalReviews,
    List<BookingDto> RecentBookings,
    List<EarningsChartDataDto> EarningsChart
);

public record MemberDashboardDto(
    int TotalBookings,
    int ActiveBookings,
    int CompletedBookings,
    decimal TotalSpent,
    List<BookingDto> UpcomingBookings,
    List<BookingDto> RecentBookings
);

public record EarningsChartDataDto(
    string Label,
    decimal Amount
);

// Common DTOs
public record ApiResponse<T>(
    bool Success,
    string? Message,
    T? Data,
    List<string>? Errors = null
);

public record PaginatedResponse<T>(
    List<T> Data,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
