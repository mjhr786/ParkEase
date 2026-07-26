namespace ParkingApp.Marketplace.Domain.Services;

/// <summary>
/// Pure EV charging session fee + idle (charger hogging) fee calculations.
/// Hourly fee = ceil(hours) × chargingRatePerHour.
/// Energy fee = kWh × ratePerKwh (Phase 2).
/// Idle fee = ceil(billable hours after end + grace) × idleRatePerHour.
/// </summary>
public static class EvChargingFeeCalculator
{
    public static decimal CalculateChargingFee(TimeSpan duration, decimal chargingRatePerHour)
    {
        if (chargingRatePerHour <= 0)
            return 0m;

        var hours = (decimal)Math.Ceiling(Math.Max(duration.TotalHours, 0));
        if (hours < 1m)
            hours = 1m;

        return Math.Round(hours * chargingRatePerHour, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Energy-based fee: delivered kWh × rate per kWh.</summary>
    public static decimal CalculateEnergyFee(decimal energyKwh, decimal ratePerKwh)
    {
        if (ratePerKwh <= 0 || energyKwh <= 0)
            return 0m;

        var kwh = Math.Round(energyKwh, 3, MidpointRounding.AwayFromZero);
        return Math.Round(kwh * ratePerKwh, 2, MidpointRounding.AwayFromZero);
    }

    public static EvIdleFeeResult CalculateIdleFee(
        DateTime endDateTimeUtc,
        DateTime asOfUtc,
        int graceMinutes,
        decimal idleRatePerHour)
    {
        if (idleRatePerHour <= 0)
            return EvIdleFeeResult.Zero;

        graceMinutes = Math.Clamp(graceMinutes, 0, 24 * 60);
        var billableStart = endDateTimeUtc.AddMinutes(graceMinutes);
        if (asOfUtc <= billableStart)
            return EvIdleFeeResult.Zero;

        var billable = asOfUtc - billableStart;
        var billableMinutes = (int)Math.Ceiling(billable.TotalMinutes);
        if (billableMinutes < 1)
            billableMinutes = 1;

        var hours = (decimal)Math.Ceiling(billableMinutes / 60d);
        if (hours < 1m)
            hours = 1m;

        var fee = Math.Round(hours * idleRatePerHour, 2, MidpointRounding.AwayFromZero);
        return new EvIdleFeeResult(fee, billableMinutes, hours);
    }
}

public readonly record struct EvIdleFeeResult(decimal Fee, int BillableMinutes, decimal BillableHoursCeil)
{
    public static EvIdleFeeResult Zero => new(0m, 0, 0m);
    public bool HasFee => Fee > 0m && BillableMinutes > 0;
}
