using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.Marketplace.Application.Interfaces;

public interface IParkingPassPricingService
{
    Task<ParkingPassPricingResult> CalculateAsync(
        Guid? userId,
        ParkingSpace parkingSpace,
        DateTime startDateUtc,
        DateTime endDateUtc,
        PricingType pricingType,
        string? discountCode = null,
        Guid? excludeBookingId = null,
        bool includeEvCharging = false,
        decimal ancillarySubtotal = 0m,
        IReadOnlyList<BookingAncillaryLineDto>? ancillaryLines = null,
        CancellationToken cancellationToken = default);
}

public sealed record ParkingPassPricingResult(
    decimal BaseAmount,
    decimal TaxAmount,
    decimal ServiceFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    string PricingDescription,
    int Duration,
    string DurationUnit,
    Guid? ParkingPassId,
    string? ParkingPassType,
    decimal? AppliedDiscountPercentage,
    bool IsPassApplied,
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


