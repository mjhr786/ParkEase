namespace ParkingApp.Marketplace.Domain.Services;

/// <summary>
/// Pure demand-based rate adjustment (no I/O).
/// Combined multiplier = occupancy × peak × weekend, clamped to [min, max].
/// Peak windows default to 07:00–10:00 and 16:00–20:00 in the facility local clock
/// (from <paramref name="timeZoneId"/>; empty/invalid → UTC).
/// </summary>
public static class DynamicPricingCalculator
{
    public const decimal DefaultMinMultiplier = 0.80m;
    public const decimal DefaultMaxMultiplier = 1.75m;
    public const decimal DefaultPeakHourMultiplier = 1.25m;
    public const decimal DefaultWeekendMultiplier = 1.15m;

    /// <summary>Morning peak start (inclusive), minutes from midnight UTC.</summary>
    public const int DefaultPeak1StartMinutes = 7 * 60;
    /// <summary>Morning peak end (exclusive).</summary>
    public const int DefaultPeak1EndMinutes = 10 * 60;
    /// <summary>Evening peak start (inclusive).</summary>
    public const int DefaultPeak2StartMinutes = 16 * 60;
    /// <summary>Evening peak end (exclusive).</summary>
    public const int DefaultPeak2EndMinutes = 20 * 60;

    public static DynamicPricingResult Calculate(
        decimal baseRate,
        bool enabled,
        int totalSpots,
        int availableSpots,
        DateTime asOfUtc,
        decimal minMultiplier = DefaultMinMultiplier,
        decimal maxMultiplier = DefaultMaxMultiplier,
        decimal peakHourMultiplier = DefaultPeakHourMultiplier,
        decimal weekendMultiplier = DefaultWeekendMultiplier,
        int peak1StartMinutes = DefaultPeak1StartMinutes,
        int peak1EndMinutes = DefaultPeak1EndMinutes,
        int peak2StartMinutes = DefaultPeak2StartMinutes,
        int peak2EndMinutes = DefaultPeak2EndMinutes,
        string? timeZoneId = null)
    {
        if (!enabled || baseRate < 0)
        {
            return DynamicPricingResult.Disabled(Math.Max(0, baseRate));
        }

        minMultiplier = ClampMultiplier(minMultiplier, 0.10m, 1.0m);
        maxMultiplier = ClampMultiplier(maxMultiplier, 1.0m, 5.0m);
        if (maxMultiplier < minMultiplier)
            (minMultiplier, maxMultiplier) = (maxMultiplier, minMultiplier);

        peakHourMultiplier = ClampMultiplier(peakHourMultiplier, 1.0m, 3.0m);
        weekendMultiplier = ClampMultiplier(weekendMultiplier, 1.0m, 3.0m);

        var localClock = ToLocalClock(asOfUtc, timeZoneId);

        var occupancyRatio = ComputeOccupancyRatio(totalSpots, availableSpots);
        var occupancyFactor = OccupancyFactor(occupancyRatio);
        var isPeak = IsPeakHour(localClock, peak1StartMinutes, peak1EndMinutes, peak2StartMinutes, peak2EndMinutes);
        var isWeekend = localClock.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        var peakFactor = isPeak ? peakHourMultiplier : 1.0m;
        var weekendFactor = isWeekend ? weekendMultiplier : 1.0m;

        var raw = occupancyFactor * peakFactor * weekendFactor;
        var multiplier = Math.Clamp(raw, minMultiplier, maxMultiplier);
        multiplier = Math.Round(multiplier, 4, MidpointRounding.AwayFromZero);

        var effective = Math.Round(baseRate * multiplier, 2, MidpointRounding.AwayFromZero);
        if (effective < 0)
            effective = 0;

        var factors = BuildFactorDescription(
            occupancyRatio, occupancyFactor, isPeak, peakFactor, isWeekend, weekendFactor,
            multiplier, minMultiplier, maxMultiplier, timeZoneId);

        return new DynamicPricingResult(
            Applied: true,
            EffectiveRate: effective,
            BaseRate: baseRate,
            Multiplier: multiplier,
            OccupancyRatio: occupancyRatio,
            IsPeakHour: isPeak,
            IsWeekend: isWeekend,
            FactorsDescription: factors);
    }

    /// <summary>Convert UTC (or unspecified-as-UTC) instant to facility local wall clock.</summary>
    public static DateTime ToLocalClock(DateTime asOfUtc, string? timeZoneId)
    {
        var utc = asOfUtc.Kind switch
        {
            DateTimeKind.Utc => asOfUtc,
            DateTimeKind.Local => asOfUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(asOfUtc, DateTimeKind.Utc)
        };

        var tz = ResolveTimeZone(timeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)
            || timeZoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase)
            || timeZoneId.Equals("Etc/UTC", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static decimal ComputeOccupancyRatio(int totalSpots, int availableSpots)
    {
        if (totalSpots <= 0)
            return 0m;

        var available = Math.Clamp(availableSpots, 0, totalSpots);
        var occupied = totalSpots - available;
        return Math.Round((decimal)occupied / totalSpots, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Step curve: empty lots discount, full lots surge.
    /// </summary>
    public static decimal OccupancyFactor(decimal occupancyRatio)
    {
        occupancyRatio = Math.Clamp(occupancyRatio, 0m, 1m);
        return occupancyRatio switch
        {
            >= 0.90m => 1.45m,
            >= 0.75m => 1.30m,
            >= 0.60m => 1.15m,
            >= 0.40m => 1.00m,
            >= 0.20m => 0.92m,
            _ => 0.85m
        };
    }

    /// <summary>Peak check against a wall-clock DateTime (local or UTC — caller supplies the right clock).</summary>
    public static bool IsPeakHour(
        DateTime localOrUtcClock,
        int peak1StartMinutes = DefaultPeak1StartMinutes,
        int peak1EndMinutes = DefaultPeak1EndMinutes,
        int peak2StartMinutes = DefaultPeak2StartMinutes,
        int peak2EndMinutes = DefaultPeak2EndMinutes)
    {
        var minutes = localOrUtcClock.Hour * 60 + localOrUtcClock.Minute;
        return InWindow(minutes, peak1StartMinutes, peak1EndMinutes)
            || InWindow(minutes, peak2StartMinutes, peak2EndMinutes);
    }

    private static bool InWindow(int minutes, int start, int end)
    {
        if (start == end)
            return false;
        if (start < end)
            return minutes >= start && minutes < end;
        // Overnight window (e.g. 22:00–06:00)
        return minutes >= start || minutes < end;
    }

    private static decimal ClampMultiplier(decimal value, decimal min, decimal max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static string BuildFactorDescription(
        decimal occupancyRatio,
        decimal occupancyFactor,
        bool isPeak,
        decimal peakFactor,
        bool isWeekend,
        decimal weekendFactor,
        decimal finalMultiplier,
        decimal minMultiplier,
        decimal maxMultiplier,
        string? timeZoneId = null)
    {
        var parts = new List<string>
        {
            $"occupancy {occupancyRatio:P0} → ×{occupancyFactor:0.##}"
        };
        if (isPeak)
            parts.Add($"peak ×{peakFactor:0.##}");
        if (isWeekend)
            parts.Add($"weekend ×{weekendFactor:0.##}");
        parts.Add($"final ×{finalMultiplier:0.####} (clamp {minMultiplier:0.##}–{maxMultiplier:0.##})");
        var tz = string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId.Trim();
        parts.Add($"tz {tz}");
        return string.Join("; ", parts);
    }
}

public readonly record struct DynamicPricingResult(
    bool Applied,
    decimal EffectiveRate,
    decimal BaseRate,
    decimal Multiplier,
    decimal OccupancyRatio,
    bool IsPeakHour,
    bool IsWeekend,
    string FactorsDescription)
{
    public static DynamicPricingResult Disabled(decimal baseRate) =>
        new(false, baseRate, baseRate, 1.0m, 0m, false, false, "static rate");
}
