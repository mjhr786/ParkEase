using System.ComponentModel.DataAnnotations;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.Marketplace.Contracts.DTOs;

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
    bool HasPendingExtension,
    Guid? ParkingPassId = null,
    string? ParkingPassType = null,
    bool IsPassApplied = false,
    decimal OverstayFeeAmount = 0,
    int OverstayBillableMinutes = 0,
    DateTime? OverstayFeeChargedAt = null,
    decimal OverstayFeePaidAmount = 0,
    decimal OverstayFeeOutstanding = 0,
    /// <summary>Digital access-pass token for QR display (null until issued).</summary>
    string? QrCode = null,
    bool IncludeEvCharging = false,
    decimal EvChargingFeeAmount = 0,
    decimal EvIdleFeeAmount = 0,
    Guid? EventParkingPackageId = null,
    decimal EvEnergyDeliveredKwh = 0,
    string? EvSessionStatus = null,
    string? EvOcppTransactionId = null,
    EvPricingMode? EvPricingMode = null,
    decimal? EvRatePerKwh = null,
    string? FacilityLevel = null,
    string? FacilityZone = null,
    string? BayLabel = null,
    ValetStatus ValetStatus = ValetStatus.None,
    DateTime? ValetRequestedAt = null,
    DateTime? ValetTargetReadyAt = null,
    DateTime? ValetReadyAt = null,
    string? ValetNotes = null,
    bool IsValetEnabled = false,
    bool IsBayGuidanceEnabled = false,
    string? IndoorGuidanceNotes = null,
    decimal AncillarySubtotal = 0,
    IReadOnlyList<BookingAncillaryLineDto>? AncillaryLines = null
)
{
    public BookingDto() : this(Guid.Empty, Guid.Empty, string.Empty, Guid.Empty, string.Empty, string.Empty, 0, 0, DateTime.MinValue, DateTime.MinValue, default, default, null, null, null, null, 0, 0, 0, 0, 0, null, default, null, null, null, null, DateTime.MinValue, null, null, false) { }
}

/// <summary>Guest digital access pass for QR / gate display (+ wallet flags).</summary>
public record BookingAccessPassDto(
    Guid BookingId,
    string? BookingReference,
    string AccessToken,
    string ParkingSpaceTitle,
    string ParkingSpaceAddress,
    DateTime StartDateTime,
    DateTime EndDateTime,
    BookingStatus Status,
    bool IsValidNow,
    string? VehicleNumber,
    string QrImageUrl,
    string Payload,
    bool AppleWalletAvailable = false,
    bool GoogleWalletAvailable = false,
    string? AppleWalletDownloadPath = null,
    string? GoogleWalletLinkPath = null,
    string? WalletStatusMessage = null,
    bool AppleWalletIsSigned = false
);

/// <summary>Binary Apple Wallet package for download.</summary>
public record AppleWalletPassFileDto(
    byte[] Content,
    string FileName,
    string ContentType,
    bool IsSigned
);

/// <summary>Google Wallet save-to-wallet URL (JWT).</summary>
public record GoogleWalletSaveLinkDto(
    string? SaveUrl,
    bool IsConfigured,
    string? Message
);

/// <summary>Result of scanning / verifying an access-pass token.</summary>
public record AccessPassVerifyResultDto(
    bool AccessGranted,
    string Decision,
    string? DenialReasonCode,
    string? DenialMessage,
    Guid? BookingId,
    string? BookingReference,
    Guid? ParkingSpaceId,
    string? ParkingSpaceTitle,
    BookingStatus? Status,
    DateTime? StartDateTime,
    DateTime? EndDateTime,
    string? VehicleNumber
);

public record VerifyAccessPassDto(
    [Required] string Token
);

public record RequestValetDto(
    string? Notes = null,
    [Range(1, 120)] int? LeadMinutes = null
);

public record AssignBayDto(
    string? FacilityLevel = null,
    string? FacilityZone = null,
    string? BayLabel = null,
    [Range(1, 1000)] int? SlotNumber = null
);

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
    string? DiscountCode,
    bool IncludeEvCharging = false,
    List<Guid>? AncillaryServiceIds = null
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
    string? DiscountCode = null,
    bool IncludeEvCharging = false,
    List<Guid>? AncillaryServiceIds = null
);

public record PriceBreakdownDto(
    decimal BaseAmount,
    decimal TaxAmount,
    decimal ServiceFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    string PricingDescription,
    int Duration,
    string DurationUnit,
    Guid? ParkingPassId = null,
    string? ParkingPassType = null,
    decimal? AppliedDiscountPercentage = null,
    bool IsPassApplied = false,
    bool DynamicPricingApplied = false,
    decimal? DynamicMultiplier = null,
    string? DynamicPricingFactors = null,
    bool IncludeEvCharging = false,
    decimal EvChargingFeeAmount = 0m,
    EvPricingMode EvPricingMode = EvPricingMode.Hourly,
    decimal EvRatePerKwh = 0m,
    decimal AncillarySubtotal = 0m,
    IReadOnlyList<BookingAncillaryLineDto>? AncillaryLines = null
);

/// <summary>OCPP-inspired EV charge session summary.</summary>
public record EvChargingSessionDto(
    Guid Id,
    Guid BookingId,
    Guid ParkingSpaceId,
    string StationId,
    int ConnectorId,
    string OcppTransactionId,
    EvChargingSessionStatus Status,
    DateTime StartedAtUtc,
    DateTime? StoppedAtUtc,
    decimal MeterStartKwh,
    decimal LastMeterKwh,
    decimal? MeterEndKwh,
    decimal EnergyDeliveredKwh,
    decimal RatePerKwh,
    decimal EnergyFeeAmount,
    string Source
);

public record StartEvChargingTransactionRequest(
    [Required] Guid BookingId,
    string? StationId = null,
    int ConnectorId = 1,
    decimal MeterStartKwh = 0m
);

public record EvMeterValuesRequest(
    [Required] string OcppTransactionId,
    [Required] decimal MeterKwh
);

public record StopEvChargingTransactionRequest(
    [Required] string OcppTransactionId,
    decimal? MeterStopKwh = null
);

/// <summary>Demo: start → meter → stop with a target energy amount.</summary>
public record SimulateEvChargingSessionRequest(
    [Required] Guid BookingId,
    [Range(0.001, 500)] decimal EnergyKwh,
    string? StationId = null,
    int ConnectorId = 1
);

public record ExtendBookingDto(
    [Required] DateTime NewEndDateTime,
    /// <summary>
    /// Optional pricing unit for the extension window (Hourly/Daily/Weekly/Monthly).
    /// When omitted, the original booking pricing type is used.
    /// </summary>
    PricingType? PricingType = null
);

/// <summary>Alias for backwards compatibility — same as ExtendBookingDto.</summary>
public record RequestExtensionDto(
    [Required] DateTime NewEndDateTime,
    PricingType? PricingType = null
);


