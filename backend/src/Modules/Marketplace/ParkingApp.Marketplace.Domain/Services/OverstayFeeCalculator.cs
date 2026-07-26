namespace ParkingApp.Marketplace.Domain.Services;

/// <summary>
/// Pure overstay fee calculation (no I/O).
/// Billable minutes = time past EndDateTime after grace; fee uses ceil(hours) * hourlyRate * multiplier.
/// </summary>
public static class OverstayFeeCalculator
{
    public static OverstayFeeResult Calculate(
        DateTime endDateTimeUtc,
        DateTime asOfUtc,
        int graceMinutes,
        decimal hourlyRate,
        decimal rateMultiplier = 1.5m,
        decimal minimumFee = 0m,
        decimal? maximumFee = null)
    {
        if (hourlyRate < 0)
            hourlyRate = 0;
        if (rateMultiplier < 0)
            rateMultiplier = 0;

        graceMinutes = Math.Clamp(graceMinutes, 0, 24 * 60);
        var billableStart = endDateTimeUtc.AddMinutes(graceMinutes);
        if (asOfUtc <= billableStart)
            return OverstayFeeResult.Zero;

        var billable = asOfUtc - billableStart;
        var billableMinutes = (int)Math.Ceiling(billable.TotalMinutes);
        if (billableMinutes < 1)
            billableMinutes = 1;

        var hours = (decimal)Math.Ceiling(billableMinutes / 60d);
        if (hours < 1m)
            hours = 1m;

        var fee = Math.Round(hours * hourlyRate * rateMultiplier, 2, MidpointRounding.AwayFromZero);
        if (fee < minimumFee)
            fee = minimumFee;
        if (maximumFee is > 0 && fee > maximumFee.Value)
            fee = maximumFee.Value;

        return new OverstayFeeResult(fee, billableMinutes, hours);
    }
}

public readonly record struct OverstayFeeResult(decimal Fee, int BillableMinutes, decimal BillableHoursCeil)
{
    public static OverstayFeeResult Zero => new(0m, 0, 0m);
    public bool HasFee => Fee > 0m && BillableMinutes > 0;
}
