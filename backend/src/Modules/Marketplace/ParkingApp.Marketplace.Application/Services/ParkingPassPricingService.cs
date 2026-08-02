using ParkingApp.Application.Interfaces;

using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.Services;

namespace ParkingApp.Marketplace.Application.Services;

internal sealed class ParkingPassPricingService : IParkingPassPricingService
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
        bool includeEvCharging = false,
        decimal ancillarySubtotal = 0m,
        IReadOnlyList<BookingAncillaryLineDto>? ancillaryLines = null,
        CancellationToken cancellationToken = default)
    {
        var duration = endDateUtc - startDateUtc;
        var (parkingBase, durationValue, durationUnit, dynamicInfo) = CalculateBaseAmount(
            parkingSpace,
            startDateUtc,
            endDateUtc,
            pricingType);

        var wantsEv = includeEvCharging && parkingSpace.HasEvCharging;
        // PerKwh: energy fee settled after charge stop (EvChargingSession). Hourly: lock at book.
        var evFee = wantsEv && parkingSpace.EvPricingMode == EvPricingMode.Hourly
            ? EvChargingFeeCalculator.CalculateChargingFee(duration, parkingSpace.EvChargingRatePerHour)
            : 0m;
        var evMode = parkingSpace.HasEvCharging ? parkingSpace.EvPricingMode : EvPricingMode.Hourly;
        var evRateKwh = parkingSpace.HasEvCharging ? parkingSpace.EvRatePerKwh : 0m;

        var safeAncillary = ancillarySubtotal < 0
            ? 0m
            : Math.Round(ancillarySubtotal, 2, MidpointRounding.AwayFromZero);

        // BaseAmount is taxable parking + optional EV + ancillary add-ons (before tax/service).
        var baseAmount = parkingBase + evFee + safeAncillary;

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
                var passDescription = BuildPricingDescription(
                    pricingType,
                    discountCode: null,
                    dynamicInfo,
                    wantsEv,
                    evFee,
                    passKind: applicablePass.PassType.Kind.ToString(),
                    evMode,
                    evRateKwh,
                    safeAncillary);

                return new ParkingPassPricingResult(
                    baseAmount,
                    taxAmount,
                    serviceFee,
                    discountAmount,
                    totalAmount,
                    passDescription,
                    durationValue,
                    durationUnit,
                    applicablePass.Id,
                    applicablePass.PassType.Kind.ToString(),
                    applicablePass.DiscountPercentage,
                    true,
                    dynamicInfo.Applied,
                    dynamicInfo.Applied ? dynamicInfo.Multiplier : null,
                    dynamicInfo.Applied ? dynamicInfo.FactorsDescription : null,
                    wantsEv,
                    evFee,
                    evMode,
                    evRateKwh,
                    safeAncillary,
                    ancillaryLines);
            }
        }

        var promoDiscount = ApplyDiscountCode(grossAmount, discountCode);
        var pricingDescription = BuildPricingDescription(
            pricingType, discountCode, dynamicInfo, wantsEv, evFee, passKind: null, evMode, evRateKwh, safeAncillary);

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
            false,
            dynamicInfo.Applied,
            dynamicInfo.Applied ? dynamicInfo.Multiplier : null,
            dynamicInfo.Applied ? dynamicInfo.FactorsDescription : null,
            wantsEv,
            evFee,
            evMode,
            evRateKwh,
            safeAncillary,
            ancillaryLines);
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

    private static (decimal amount, int duration, string unit, DynamicPricingResult dynamicInfo) CalculateBaseAmount(
        ParkingSpace parkingSpace,
        DateTime startDateUtc,
        DateTime endDateUtc,
        PricingType pricingType)
    {
        // Hourly: bill by clock hours (partial hour rounds up).
        // Daily / Weekly / Monthly: bill by full calendar days in the facility timezone
        // so start/end clock times do not change the day count (e.g. Jul 26 5pm → Jul 28 6pm
        // is the same as Jul 26 → Jul 28 by date only).
        var (listRate, durationValue, durationUnit) = pricingType switch
        {
            PricingType.Hourly => (
                parkingSpace.HourlyRate,
                Math.Max(1, (int)Math.Ceiling((endDateUtc - startDateUtc).TotalHours)),
                "hours"
            ),
            PricingType.Daily => (
                parkingSpace.DailyRate,
                GetBillableCalendarDays(startDateUtc, endDateUtc, parkingSpace.TimeZoneId),
                "days"
            ),
            PricingType.Weekly => (
                parkingSpace.WeeklyRate,
                Math.Max(1, (int)Math.Ceiling(
                    GetBillableCalendarDays(startDateUtc, endDateUtc, parkingSpace.TimeZoneId) / 7.0)),
                "weeks"
            ),
            PricingType.Monthly => (
                parkingSpace.MonthlyRate,
                Math.Max(1, (int)Math.Ceiling(
                    GetBillableCalendarDays(startDateUtc, endDateUtc, parkingSpace.TimeZoneId) / 30.0)),
                "months"
            ),
            _ => (
                parkingSpace.HourlyRate,
                Math.Max(1, (int)Math.Ceiling((endDateUtc - startDateUtc).TotalHours)),
                "hours"
            )
        };

        var dynamicInfo = DynamicPricingCalculator.Calculate(
            listRate,
            parkingSpace.IsDynamicPricingEnabled,
            parkingSpace.TotalSpots,
            parkingSpace.AvailableSpots,
            startDateUtc,
            parkingSpace.DynamicMinMultiplier,
            parkingSpace.DynamicMaxMultiplier,
            parkingSpace.PeakHourMultiplier,
            parkingSpace.WeekendMultiplier,
            timeZoneId: parkingSpace.TimeZoneId);

        var unitRate = dynamicInfo.EffectiveRate;
        var amount = Math.Round(unitRate * durationValue, 2, MidpointRounding.AwayFromZero);
        return (amount, durationValue, durationUnit, dynamicInfo);
    }

    /// <summary>
    /// Inclusive calendar-day span in the facility timezone. Clock times are ignored:
    /// same local date → 1 day; Jul 26 → Jul 28 → 3 days (26, 27, and 28 each count as a full day).
    /// </summary>
    internal static int GetBillableCalendarDays(DateTime startUtc, DateTime endUtc, string? timeZoneId)
    {
        var startLocal = DynamicPricingCalculator.ToLocalClock(startUtc, timeZoneId);
        var endLocal = DynamicPricingCalculator.ToLocalClock(endUtc, timeZoneId);

        if (endLocal < startLocal)
            return 1;

        var days = (endLocal.Date - startLocal.Date).Days + 1;
        return days < 1 ? 1 : days;
    }

    /// <summary>
    /// Whether the pricing unit bills whole calendar days (clock times ignored).
    /// </summary>
    internal static bool IsDayBasedPricing(PricingType pricingType) =>
        pricingType is PricingType.Daily or PricingType.Weekly or PricingType.Monthly;

    /// <summary>
    /// Start of the billable extension window.
    /// <list type="bullet">
    /// <item>Hourly: continuous from the booking end instant.</item>
    /// <item>Daily/Weekly/Monthly: midnight of the next local calendar day after the day
    /// already covered by <paramref name="bookingEndUtc"/>. Inclusive day billing would
    /// otherwise double-count the current end day (e.g. end 4 Aug → extend to 5 Aug must be 1 day, not 2).</item>
    /// </list>
    /// </summary>
    internal static DateTime GetExtensionPricingStartUtc(
        DateTime bookingEndUtc,
        PricingType pricingType,
        string? timeZoneId)
    {
        if (!IsDayBasedPricing(pricingType))
        {
            return bookingEndUtc.Kind == DateTimeKind.Utc
                ? bookingEndUtc
                : DateTime.SpecifyKind(bookingEndUtc, DateTimeKind.Utc);
        }

        var endLocal = DynamicPricingCalculator.ToLocalClock(bookingEndUtc, timeZoneId);
        var nextDayLocalMidnight = endLocal.Date.AddDays(1);
        return DynamicPricingCalculator.FromLocalClock(nextDayLocalMidnight, timeZoneId);
    }

    private static string BuildPricingDescription(
        PricingType pricingType,
        string? discountCode,
        DynamicPricingResult dynamicInfo,
        bool includeEv = false,
        decimal evFee = 0m,
        string? passKind = null,
        EvPricingMode evMode = EvPricingMode.Hourly,
        decimal evRateKwh = 0m,
        decimal ancillarySubtotal = 0m)
    {
        var baseText = passKind is not null
            ? $"{passKind} pass applied"
            : string.IsNullOrWhiteSpace(discountCode)
                ? $"{pricingType} rate applied"
                : $"{pricingType} rate applied with promo discount";

        if (dynamicInfo.Applied)
            baseText += $"; dynamic pricing ×{dynamicInfo.Multiplier:0.####}";

        if (includeEv && evMode == EvPricingMode.PerKwh)
            baseText += evRateKwh > 0
                ? $"; EV energy billed after charge at ₹{evRateKwh:0.00}/kWh"
                : "; EV energy billed after charge (kWh)";
        else if (includeEv && evFee > 0)
            baseText += $"; EV charging ₹{evFee:0.00}";
        else if (includeEv)
            baseText += "; EV charging included (no surcharge)";

        if (ancillarySubtotal > 0)
            baseText += $"; add-ons ₹{ancillarySubtotal:0.00}";

        return baseText;
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


