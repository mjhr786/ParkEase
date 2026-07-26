using FluentAssertions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Services;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class EvChargingFeeCalculatorTests
{
    [Fact]
    public void CalculateChargingFee_CeilHoursTimesRate()
    {
        var fee = EvChargingFeeCalculator.CalculateChargingFee(TimeSpan.FromMinutes(90), 20m);
        fee.Should().Be(40m); // 2 hours * 20
    }

    [Fact]
    public void CalculateIdleFee_Zero_WithinGrace()
    {
        var end = new DateTime(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);
        var asOf = end.AddMinutes(10);
        var result = EvChargingFeeCalculator.CalculateIdleFee(end, asOf, graceMinutes: 15, idleRatePerHour: 50m);
        result.HasFee.Should().BeFalse();
    }

    [Fact]
    public void CalculateIdleFee_AfterGrace_CeilHours()
    {
        var end = new DateTime(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);
        // grace 15 → billable from 10:15; asOf 11:20 → 65 min → 2 hours * 50
        var asOf = new DateTime(2026, 7, 25, 11, 20, 0, DateTimeKind.Utc);
        var result = EvChargingFeeCalculator.CalculateIdleFee(end, asOf, 15, 50m);
        result.Fee.Should().Be(100m);
        result.BillableMinutes.Should().Be(65);
    }

    [Fact]
    public void SetEvCharging_SyncsAmenityAndRates()
    {
        var parking = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "D", "A", "C", "S", "IN", "1",
            0, 0, ParkingApp.Marketplace.Contracts.Enums.ParkingType.Open,
            5, 10, 10, 10, 10);

        parking.SetEvCharging(true, chargerCount: 2, chargingRatePerHour: 30m, idleRatePerHour: 60m, idleGraceMinutes: 10);

        parking.HasEvCharging.Should().BeTrue();
        parking.EvChargerCount.Should().Be(2);
        parking.EvChargingRatePerHour.Should().Be(30m);
        parking.EvIdleRatePerHour.Should().Be(60m);
        parking.EvIdleGraceMinutes.Should().Be(10);
        parking.Amenities.Should().Contain("EV Charging");

        parking.SetEvCharging(false);
        parking.HasEvCharging.Should().BeFalse();
        parking.Amenities.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ApplyEvIdleFee_IncreasesTotal_Once()
    {
        var booking = new Booking
        {
            Status = ParkingApp.Marketplace.Contracts.Enums.BookingStatus.InProgress,
            IncludeEvCharging = true,
            TotalAmount = 100m
        };

        booking.ApplyEvIdleFee(40m, DateTime.UtcNow).Should().BeTrue();
        booking.EvIdleFeeAmount.Should().Be(40m);
        booking.TotalAmount.Should().Be(140m);
        booking.ApplyEvIdleFee(40m, DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void CalculateEnergyFee_KwhTimesRate()
    {
        EvChargingFeeCalculator.CalculateEnergyFee(12.5m, 15m).Should().Be(187.50m);
        EvChargingFeeCalculator.CalculateEnergyFee(0m, 15m).Should().Be(0m);
        EvChargingFeeCalculator.CalculateEnergyFee(10m, 0m).Should().Be(0m);
    }

    [Fact]
    public void EvChargingSession_Stop_SettlesEnergyAndBookingFee()
    {
        var booking = new Booking
        {
            Status = ParkingApp.Marketplace.Contracts.Enums.BookingStatus.InProgress,
            IncludeEvCharging = true,
            TotalAmount = 200m,
            BaseAmount = 150m,
            EvChargingFeeAmount = 0m
        };

        var session = EvChargingSession.Start(
            booking.Id == Guid.Empty ? Guid.NewGuid() : booking.Id,
            Guid.NewGuid(),
            "MOCK-TX-1",
            ratePerKwh: 15m,
            meterStartKwh: 100m);

        session.RecordMeterValue(106.25m);
        var fee = session.Stop(112.5m);

        fee.Should().Be(187.50m); // 12.5 * 15
        session.EnergyDeliveredKwh.Should().Be(12.5m);
        session.Status.Should().Be(ParkingApp.Marketplace.Contracts.Enums.EvChargingSessionStatus.Completed);

        booking.ApplyEvEnergyFee(fee).Should().BeTrue();
        booking.EvChargingFeeAmount.Should().Be(187.50m);
        booking.TotalAmount.Should().Be(387.50m);
        booking.ApplyEvEnergyFee(fee).Should().BeFalse();
    }

    [Fact]
    public void SetEvCharging_PerKwhMode()
    {
        var parking = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "D", "A", "C", "S", "IN", "1",
            0, 0, ParkingApp.Marketplace.Contracts.Enums.ParkingType.Open,
            5, 10, 10, 10, 10);

        parking.SetEvCharging(
            true,
            chargerCount: 2,
            chargingRatePerHour: 0m,
            idleRatePerHour: 50m,
            idleGraceMinutes: 15,
            pricingMode: ParkingApp.Marketplace.Contracts.Enums.EvPricingMode.PerKwh,
            ratePerKwh: 18m);

        parking.EvPricingMode.Should().Be(ParkingApp.Marketplace.Contracts.Enums.EvPricingMode.PerKwh);
        parking.EvRatePerKwh.Should().Be(18m);
    }
}
