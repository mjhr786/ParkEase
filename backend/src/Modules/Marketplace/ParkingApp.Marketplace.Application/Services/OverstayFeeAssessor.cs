using Microsoft.Extensions.Options;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Services;

namespace ParkingApp.Marketplace.Application.Services;

/// <summary>
/// Shared helper to assess/increase overstay fees on a booking (background + check-out).
/// </summary>
internal static class OverstayFeeAssessor
{
    public static bool TryAssess(
        Booking booking,
        LprOverstayOptions options,
        DateTime asOfUtc,
        out OverstayFeeResult calculation)
    {
        calculation = OverstayFeeResult.Zero;

        if (!options.FeesEnabled)
            return false;
        if (booking.ParkingSpace is null)
            return false;
        if (asOfUtc <= booking.EndDateTime.AddMinutes(Math.Clamp(options.GraceMinutes, 0, 24 * 60)))
            return false;

        calculation = OverstayFeeCalculator.Calculate(
            booking.EndDateTime,
            asOfUtc,
            options.GraceMinutes,
            booking.ParkingSpace.HourlyRate,
            options.RateMultiplier,
            options.MinimumFee,
            options.MaximumFee is > 0 ? options.MaximumFee : null);

        if (!calculation.HasFee)
            return false;

        return booking.ApplyOverstayFee(calculation.Fee, calculation.BillableMinutes, asOfUtc);
    }
}
