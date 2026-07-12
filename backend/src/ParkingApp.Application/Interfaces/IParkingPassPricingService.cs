using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.Interfaces;

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
    bool IsPassApplied
);
