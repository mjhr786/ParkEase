using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.Services;

public class ParkingPassPricingService : IParkingPassPricingService
{
    private const decimal TaxRate = 0.18m;
    private const decimal ServiceFeeRate = 0.05m;
    private static readonly IReadOnlyDictionary<DateOnly, decimal> EmptyBookedHoursByDay = new Dictionary<DateOnly, decimal>();

    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public ParkingPassPricingService(IMarketplaceUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ParkingPassPricingResult> CalculateAsync(
        Guid? userId,
        ParkingSpace parkingSpace,
        DateTime startDateUtc,
        DateTime endDateUtc,
        PricingType pricingType,
        string? discountCode = null,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default)
    {
        var duration = endDateUtc - startDateUtc;
        var (baseAmount, durationValue, durationUnit) = CalculateBaseAmount(parkingSpace, duration, pricingType);

        var taxAmount = Math.Round(baseAmount * TaxRate, 2, MidpointRounding.AwayFromZero);
        var serviceFee = Math.Round(baseAmount * ServiceFeeRate, 2, MidpointRounding.AwayFromZero);
        var grossAmount = baseAmount + taxAmount + serviceFee;

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            var applicablePass = await ResolveApplicablePassAsync(
                userId.Value,
                parkingSpace,
                startDateUtc,
                endDateUtc,
                excludeBookingId,
                cancellationToken);

            if (applicablePass != null)
            {
                var discountAmount = applicablePass.CalculateDiscountAmount(grossAmount);
                var totalAmount = Math.Max(0, grossAmount - discountAmount);

                return new ParkingPassPricingResult(
                    baseAmount,
                    taxAmount,
                    serviceFee,
                    discountAmount,
                    totalAmount,
                    $"{applicablePass.PassType.Kind} pass applied",
                    durationValue,
                    durationUnit,
                    applicablePass.Id,
                    applicablePass.PassType.Kind.ToString(),
                    applicablePass.DiscountPercentage,
                    true);
            }
        }

        var promoDiscount = ApplyDiscountCode(grossAmount, discountCode);
        var pricingDescription = string.IsNullOrWhiteSpace(discountCode)
            ? $"{pricingType} rate applied"
            : $"{pricingType} rate applied with promo discount";

        return new ParkingPassPricingResult(
            baseAmount,
            taxAmount,
            serviceFee,
            promoDiscount,
            Math.Max(0, grossAmount - promoDiscount),
            pricingDescription,
            durationValue,
            durationUnit,
            null,
            null,
            null,
            false);
    }

    private async Task<ParkingPass?> ResolveApplicablePassAsync(
        Guid userId,
        ParkingSpace parkingSpace,
        DateTime startDateUtc,
        DateTime endDateUtc,
        Guid? excludeBookingId,
        CancellationToken cancellationToken)
    {
        var parkingPassRepository = _unitOfWork.ParkingPasses;
        if (parkingPassRepository is null)
        {
            return null;
        }

        var candidatePassesTask = parkingPassRepository.GetCandidatePassesForBookingAsync(
            userId,
            parkingSpace.Id,
            parkingSpace.ZoneCode,
            startDateUtc,
            endDateUtc,
            cancellationToken);
        if (candidatePassesTask is null)
        {
            return null;
        }

        var candidatePasses = await candidatePassesTask;
        if (candidatePasses == null || candidatePasses.Count == 0)
        {
            return null;
        }

        // One query for all candidate passes (was N GetBookedHoursByDayAsync).
        var hoursByPass = await parkingPassRepository.GetBookedHoursByDayForPassesAsync(
            candidatePasses.Select(p => p.Id).ToList(),
            userId,
            startDateUtc,
            endDateUtc,
            excludeBookingId,
            cancellationToken) ?? new Dictionary<Guid, IReadOnlyDictionary<DateOnly, decimal>>();

        foreach (var candidatePass in candidatePasses)
        {
            var existingHoursByDay = hoursByPass.TryGetValue(candidatePass.Id, out var hours)
                ? hours
                : EmptyBookedHoursByDay;

            if (candidatePass.IsValidForBooking(parkingSpace, startDateUtc, endDateUtc, existingHoursByDay, DateTime.UtcNow))
            {
                return candidatePass;
            }
        }

        return null;
    }

    private static (decimal amount, int duration, string unit) CalculateBaseAmount(
        ParkingSpace parkingSpace,
        TimeSpan duration,
        PricingType pricingType)
    {
        return pricingType switch
        {
            PricingType.Hourly => (
                parkingSpace.HourlyRate * (decimal)Math.Ceiling(duration.TotalHours),
                (int)Math.Ceiling(duration.TotalHours),
                "hours"
            ),
            PricingType.Daily => (
                parkingSpace.DailyRate * (decimal)Math.Ceiling(duration.TotalDays),
                (int)Math.Ceiling(duration.TotalDays),
                "days"
            ),
            PricingType.Weekly => (
                parkingSpace.WeeklyRate * (decimal)Math.Ceiling(duration.TotalDays / 7),
                (int)Math.Ceiling(duration.TotalDays / 7),
                "weeks"
            ),
            PricingType.Monthly => (
                parkingSpace.MonthlyRate * (decimal)Math.Ceiling(duration.TotalDays / 30),
                (int)Math.Ceiling(duration.TotalDays / 30),
                "months"
            ),
            _ => (
                parkingSpace.HourlyRate * (decimal)Math.Ceiling(duration.TotalHours),
                (int)Math.Ceiling(duration.TotalHours),
                "hours"
            )
        };
    }

    private static decimal ApplyDiscountCode(decimal grossAmount, string? discountCode)
    {
        if (string.IsNullOrWhiteSpace(discountCode))
        {
            return 0;
        }

        return discountCode.Trim().ToUpperInvariant() switch
        {
            "FIRST10" => Math.Round(grossAmount * 0.10m, 2, MidpointRounding.AwayFromZero),
            "PARK20" => Math.Round(grossAmount * 0.20m, 2, MidpointRounding.AwayFromZero),
            "SAVE50" => Math.Min(50m, grossAmount),
            _ => 0
        };
    }
}
