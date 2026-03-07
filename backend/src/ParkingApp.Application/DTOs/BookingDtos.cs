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
    double Latitude,
    double Longitude,
    DateTime StartDateTime,
    DateTime EndDateTime,
    PricingType PricingType,
    VehicleType VehicleType,
    int? SlotNumber,
    string? VehicleNumber,
    string? VehicleModel,
    string? VehicleColor,
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
    DateTime CreatedAt,
    // Extension request fields
    DateTime? PendingExtensionEndDateTime,
    decimal? PendingExtensionAmount,
    bool HasPendingExtension
)
{
    public BookingDto() : this(Guid.Empty, Guid.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty, 0, 0, DateTime.MinValue, DateTime.MinValue, default, default, null, null, null, null, 0, 0, 0, 0, 0, null, default, null, null, null, null, DateTime.MinValue, null, null, false) { }
}

public record CreateBookingDto(
    [Required] Guid ParkingSpaceId,
    [Required] DateTime StartDateTime,
    [Required] DateTime EndDateTime,
    [Required] PricingType PricingType,
    [Required] VehicleType VehicleType,
    [Range(1, 1000)] int? SlotNumber,
    string? VehicleNumber,
    string? VehicleModel,
    string? VehicleColor,
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

public record ExtendBookingDto(
    [Required] DateTime NewEndDateTime
);

/// <summary>Alias for backwards compatibility — same as ExtendBookingDto.</summary>
public record RequestExtensionDto(
    [Required] DateTime NewEndDateTime
);
