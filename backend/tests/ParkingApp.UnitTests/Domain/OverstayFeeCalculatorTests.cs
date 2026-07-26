using FluentAssertions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Marketplace.Domain.Services;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class OverstayFeeCalculatorTests
{
    [Fact]
    public void Calculate_WithinGrace_ReturnsZero()
    {
        var end = DateTime.UtcNow.AddMinutes(-10);
        var result = OverstayFeeCalculator.Calculate(end, DateTime.UtcNow, graceMinutes: 15, hourlyRate: 100m);

        result.HasFee.Should().BeFalse();
        result.Fee.Should().Be(0);
    }

    [Fact]
    public void Calculate_AfterGrace_UsesCeilHoursTimesMultiplier()
    {
        var end = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        // 15 min grace → billable from 10:15; asOf 11:20 → 65 min → 2 ceil hours
        var asOf = new DateTime(2026, 1, 1, 11, 20, 0, DateTimeKind.Utc);

        var result = OverstayFeeCalculator.Calculate(
            end, asOf, graceMinutes: 15, hourlyRate: 100m, rateMultiplier: 1.5m);

        result.BillableMinutes.Should().Be(65);
        result.BillableHoursCeil.Should().Be(2m);
        result.Fee.Should().Be(300m); // 2 * 100 * 1.5
    }

    [Fact]
    public void Calculate_AppliesMinimumAndMaximum()
    {
        var end = DateTime.UtcNow.AddHours(-1);
        // 1 ceil hour * 10 * 1 = 10 → minimum raises to 50
        var minResult = OverstayFeeCalculator.Calculate(
            end, DateTime.UtcNow, 0, hourlyRate: 10m, rateMultiplier: 1m,
            minimumFee: 50m, maximumFee: 80m);
        minResult.Fee.Should().Be(50m);

        // 10 ceil hours * 20 * 1 = 200 → maximum caps to 80
        var maxResult = OverstayFeeCalculator.Calculate(
            DateTime.UtcNow.AddHours(-10), DateTime.UtcNow, 0,
            hourlyRate: 20m, rateMultiplier: 1m, minimumFee: 0m, maximumFee: 80m);
        maxResult.Fee.Should().Be(80m);
    }

    [Fact]
    public void ApplyOverstayFee_IncreasesTotalAndRaisesEvent()
    {
        var booking = new Booking
        {
            Status = BookingStatus.InProgress,
            TotalAmount = 100m,
            EndDateTime = DateTime.UtcNow.AddHours(-1)
        };

        booking.ApplyOverstayFee(50m, 60, DateTime.UtcNow).Should().BeTrue();
        booking.OverstayFeeAmount.Should().Be(50m);
        booking.TotalAmount.Should().Be(150m);
        booking.DomainEvents.Should().ContainSingle(e => e is BookingOverstayFeeAssessedEvent);

        booking.ApplyOverstayFee(50m, 60, DateTime.UtcNow).Should().BeFalse();
        booking.ApplyOverstayFee(75m, 90, DateTime.UtcNow).Should().BeTrue();
        booking.TotalAmount.Should().Be(175m);
        booking.OverstayFeeAmount.Should().Be(75m);
    }

    [Fact]
    public void MarkOverstayFeePaid_ReducesOutstanding()
    {
        var booking = new Booking
        {
            Status = BookingStatus.InProgress,
            TotalAmount = 100m,
            EndDateTime = DateTime.UtcNow.AddHours(-1)
        };
        booking.ApplyOverstayFee(50m, 60, DateTime.UtcNow);

        booking.OverstayFeeOutstanding.Should().Be(50m);
        booking.MarkOverstayFeePaid(50m, "pi_test", DateTime.UtcNow);
        booking.OverstayFeePaidAmount.Should().Be(50m);
        booking.OverstayFeeOutstanding.Should().Be(0m);
        booking.OverstayFeeTransactionId.Should().Be("pi_test");
        booking.DomainEvents.Should().Contain(e => e is BookingOverstayFeePaidEvent);
    }
}
