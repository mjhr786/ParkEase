using FluentAssertions;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Services;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

/// <summary>
/// Day-based extension windows must not double-count the already-paid booking end day
/// when inclusive calendar-day billing is used (e.g. end 4 Aug → extend to 5 Aug = 1 day).
/// </summary>
public class ExtensionPricingStartTests
{
    [Fact]
    public void GetExtensionPricingStartUtc_Daily_StartsNextLocalCalendarDay()
    {
        // Booking ends end-of-day 4 Aug 2026 UTC
        var bookingEnd = new DateTime(2026, 8, 4, 23, 59, 0, DateTimeKind.Utc);

        var start = ParkingPassPricingService.GetExtensionPricingStartUtc(
            bookingEnd,
            PricingType.Daily,
            "UTC");

        start.Should().Be(new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetExtensionPricingStartUtc_Hourly_ContinuesFromBookingEnd()
    {
        var bookingEnd = new DateTime(2026, 8, 4, 14, 30, 0, DateTimeKind.Utc);

        var start = ParkingPassPricingService.GetExtensionPricingStartUtc(
            bookingEnd,
            PricingType.Hourly,
            "UTC");

        start.Should().Be(bookingEnd);
    }

    [Fact]
    public void DayBasedExtension_Aug4ToAug5_BillsOneDay_NotTwo()
    {
        var bookingEnd = new DateTime(2026, 8, 4, 23, 59, 0, DateTimeKind.Utc);
        var newEnd = new DateTime(2026, 8, 5, 23, 59, 59, DateTimeKind.Utc);

        // Bug before fix: inclusive days from bookingEnd → newEnd counted Aug 4 and Aug 5 (= 2).
        var wrongDays = ParkingPassPricingService.GetBillableCalendarDays(bookingEnd, newEnd, "UTC");
        wrongDays.Should().Be(2);

        var pricingStart = ParkingPassPricingService.GetExtensionPricingStartUtc(
            bookingEnd,
            PricingType.Daily,
            "UTC");
        var correctDays = ParkingPassPricingService.GetBillableCalendarDays(pricingStart, newEnd, "UTC");
        correctDays.Should().Be(1);
    }

    [Fact]
    public void DayBasedExtension_Aug4ToAug6_BillsTwoDays()
    {
        var bookingEnd = new DateTime(2026, 8, 4, 23, 59, 0, DateTimeKind.Utc);
        var newEnd = new DateTime(2026, 8, 6, 23, 59, 59, DateTimeKind.Utc);

        var pricingStart = ParkingPassPricingService.GetExtensionPricingStartUtc(
            bookingEnd,
            PricingType.Daily,
            "UTC");
        var days = ParkingPassPricingService.GetBillableCalendarDays(pricingStart, newEnd, "UTC");
        days.Should().Be(2);
    }

    [Fact]
    public void FromLocalClock_RoundTripsWithToLocalClock_Utc()
    {
        var localMidnight = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Unspecified);
        var utc = DynamicPricingCalculator.FromLocalClock(localMidnight, "UTC");
        utc.Should().Be(new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc));

        var back = DynamicPricingCalculator.ToLocalClock(utc, "UTC");
        back.Date.Should().Be(new DateTime(2026, 8, 5));
    }

    [Theory]
    [InlineData(PricingType.Daily)]
    [InlineData(PricingType.Weekly)]
    [InlineData(PricingType.Monthly)]
    public void IsDayBasedPricing_True_ForCalendarUnits(PricingType type)
    {
        ParkingPassPricingService.IsDayBasedPricing(type).Should().BeTrue();
    }

    [Fact]
    public void IsDayBasedPricing_False_ForHourly()
    {
        ParkingPassPricingService.IsDayBasedPricing(PricingType.Hourly).Should().BeFalse();
    }
}
