using System.ComponentModel.DataAnnotations;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.DTOs;

public record BookingDto(
    Guid Id,
    Guid UserId,
    string UserName,
    Guid ParkingSpaceId,
    string ParkingSpaceTitle,
    string ParkingSpaceAddress,
    DateTime StartDateTime,
    DateTime EndDateTime,
    PricingType PricingType,
    VehicleType VehicleType,
    string? VehicleNumber,
    string? VehicleModel,
    decimal BaseAmount,
    decimal TaxAmount,
    decimal ServiceFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    string? DiscountCode,
    BookingStatus Status,
    string? BookingReference,
    DateTime? CheckInTime,
    DateTime? CheckOutTime,
    PaymentStatus? PaymentStatus,
    DateTime CreatedAt
);

public record CreateBookingDto(
    [Required] Guid ParkingSpaceId,
    [Required] DateTime StartDateTime,
    [Required] DateTime EndDateTime,
    [Required] PricingType PricingType,
    [Required] VehicleType VehicleType,
    string? VehicleNumber,
    string? VehicleModel,
    string? DiscountCode
);

public record UpdateBookingDto(
    DateTime? StartDateTime,
    DateTime? EndDateTime,
    VehicleType? VehicleType,
    string? VehicleNumber,
    string? VehicleModel
);

public record BookingFilterDto(
    Guid? UserId = null,
    Guid? ParkingSpaceId = null,
    BookingStatus? Status = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Page = 1,
    int PageSize = 20
);

public record BookingListResultDto(
    List<BookingDto> Bookings,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record CancelBookingDto(
    [Required] string Reason
);

public record CheckInDto(
    [Required] string BookingReference
);

public record RejectBookingDto(
    string? Reason
);

public record PriceCalculationDto(
    Guid ParkingSpaceId,
    DateTime StartDateTime,
    DateTime EndDateTime,
    PricingType PricingType,
    string? DiscountCode = null
);

public record PriceBreakdownDto(
    decimal BaseAmount,
    decimal TaxAmount,
    decimal ServiceFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    string PricingDescription,
    int Duration,
    string DurationUnit
);
