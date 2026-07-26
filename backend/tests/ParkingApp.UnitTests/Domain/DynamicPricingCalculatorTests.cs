using ParkingApp.Marketplace.Domain.Services;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class DynamicPricingCalculatorTests
{
    [Fact]
    public void Disabled_Returns_Base_Rate_Unchanged()
    {
        var result = DynamicPricingCalculator.Calculate(
            baseRate: 100m,
            enabled: false,
            totalSpots: 10,
            availableSpots: 1,
            asOfUtc: new DateTime(2026, 7, 25, 8, 0, 0, DateTimeKind.Utc));

        Assert.False(result.Applied);
        Assert.Equal(100m, result.EffectiveRate);
        Assert.Equal(1.0m, result.Multiplier);
    }

    [Fact]
    public void High_Occupancy_Applies_Surge_Factor()
    {
        // 9/10 occupied → 0.9 occupancy → 1.45 occupancy factor
        // weekday 12:00 (not peak) → combined 1.45, within default max 1.75
        var result = DynamicPricingCalculator.Calculate(
            baseRate: 100m,
            enabled: true,
            totalSpots: 10,
            availableSpots: 1,
            asOfUtc: new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc)); // Wednesday noon

        Assert.True(result.Applied);
        Assert.False(result.IsPeakHour);
        Assert.False(result.IsWeekend);
        Assert.Equal(0.9m, result.OccupancyRatio);
        Assert.Equal(1.45m, result.Multiplier);
        Assert.Equal(145m, result.EffectiveRate);
    }

    [Fact]
    public void Peak_And_Weekend_Stack_Then_Clamp_To_Max()
    {
        // occupancy 0.9 → 1.45; peak 1.25; weekend 1.15 → raw ~2.08 → clamp 1.75
        var result = DynamicPricingCalculator.Calculate(
            baseRate: 100m,
            enabled: true,
            totalSpots: 10,
            availableSpots: 1,
            asOfUtc: new DateTime(2026, 7, 25, 8, 30, 0, DateTimeKind.Utc), // Saturday morning peak
            maxMultiplier: 1.75m);

        Assert.True(result.Applied);
        Assert.True(result.IsPeakHour);
        Assert.True(result.IsWeekend);
        Assert.Equal(1.75m, result.Multiplier);
        Assert.Equal(175m, result.EffectiveRate);
    }

    [Fact]
    public void Low_Occupancy_Applies_Discount()
    {
        // 0 occupied → occupancy factor 0.85, midday weekday
        var result = DynamicPricingCalculator.Calculate(
            baseRate: 100m,
            enabled: true,
            totalSpots: 10,
            availableSpots: 10,
            asOfUtc: new DateTime(2026, 7, 22, 13, 0, 0, DateTimeKind.Utc));

        Assert.True(result.Applied);
        Assert.Equal(0m, result.OccupancyRatio);
        Assert.Equal(0.85m, result.Multiplier);
        Assert.Equal(85m, result.EffectiveRate);
    }

    [Fact]
    public void Local_TimeZone_Affects_Peak_Windows()
    {
        // 02:30 UTC Saturday = 08:00 Asia/Kolkata (UTC+5:30) → local peak morning
        var asOfUtc = new DateTime(2026, 7, 25, 2, 30, 0, DateTimeKind.Utc);
        var utcResult = DynamicPricingCalculator.Calculate(
            100m, true, 10, 10, asOfUtc, timeZoneId: "UTC");
        var indiaResult = DynamicPricingCalculator.Calculate(
            100m, true, 10, 10, asOfUtc, timeZoneId: "Asia/Kolkata");

        Assert.False(utcResult.IsPeakHour); // 02:30 UTC not peak
        Assert.True(indiaResult.IsPeakHour); // 08:00 IST is peak
        Assert.Contains("tz Asia/Kolkata", indiaResult.FactorsDescription);
    }

    [Fact]
    public void ToLocalClock_Utc_Unchanged_For_Utc_Zone()
    {
        var utc = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var local = DynamicPricingCalculator.ToLocalClock(utc, "UTC");
        Assert.Equal(12, local.Hour);
    }

    [Theory]
    [InlineData(7, 0, true)]
    [InlineData(9, 59, true)]
    [InlineData(10, 0, false)]
    [InlineData(16, 0, true)]
    [InlineData(19, 59, true)]
    [InlineData(20, 0, false)]
    [InlineData(12, 0, false)]
    public void Peak_Windows_Match_Defaults(int hour, int minute, bool expectedPeak)
    {
        var asOf = new DateTime(2026, 7, 22, hour, minute, 0, DateTimeKind.Utc);
        Assert.Equal(expectedPeak, DynamicPricingCalculator.IsPeakHour(asOf));
    }

    [Fact]
    public void Occupancy_Ratio_Clamps_Available_Spots()
    {
        Assert.Equal(0m, DynamicPricingCalculator.ComputeOccupancyRatio(10, 20));
        Assert.Equal(1m, DynamicPricingCalculator.ComputeOccupancyRatio(10, -5));
        Assert.Equal(0m, DynamicPricingCalculator.ComputeOccupancyRatio(0, 0));
    }
}
