using System.ComponentModel.DataAnnotations;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.Marketplace.Contracts.DTOs;

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


