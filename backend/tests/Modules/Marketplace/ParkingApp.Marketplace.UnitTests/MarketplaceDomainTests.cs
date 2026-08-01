using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Services;
using ParkingApp.Marketplace.Domain.ValueObjects;

namespace ParkingApp.Marketplace.UnitTests;

public class MarketplaceDomainTests
{
    [Fact]
    public void ParkingSpace_CreateForCompany_IsCompanyOwnedAndCorporateOnly()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var space = ParkingSpace.CreateForCompany(
            adminId,
            companyId,
            "HQ Lot",
            "Desc",
            "1 Main",
            "City",
            "ST",
            "Country",
            "00000",
            12.0,
            77.0,
            ParkingType.Covered,
            20,
            10,
            100,
            500,
            2000);

        space.CompanyOwnerId.Should().Be(companyId);
        space.OwnershipType.Should().Be(ParkingSpaceOwnershipType.CompanyOwned);
        space.IsCorporateOnly.Should().BeTrue();
        space.IsVerified.Should().BeTrue();
        space.IsActive.Should().BeTrue();
        space.OwnerId.Should().Be(adminId);
    }

    [Fact]
    public void ParkingSpace_ToggleActive_And_Retire()
    {
        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(), "Lot", "D", "1", "C", "S", "Co", "1",
            1, 1, ParkingType.Open, 5, 10, 20, 50, 100);
        space.IsActive.Should().BeTrue();
        space.ToggleActive();
        space.IsActive.Should().BeFalse();
        space.ToggleActive();
        space.IsActive.Should().BeTrue();

        space.Retire(Guid.NewGuid());
        space.IsActive.Should().BeFalse();
        space.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Booking_CreateCorporateEmployee_IsConfirmed()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddHours(2);

        var booking = Booking.CreateCorporateEmployee(
            userId, spaceId, start, end, VehicleType.Car, 150m, "KA01AB1234");

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.UserId.Should().Be(userId);
        booking.ParkingSpaceId.Should().Be(spaceId);
        booking.TotalAmount.Should().Be(150m);
        booking.VehicleNumber.Should().NotBeNullOrWhiteSpace();
        booking.BookingReference.Should().StartWith("CORP");
        booking.QRCode.Should().StartWith("CORP-");
        booking.IsCorporateStaged.Should().BeTrue();
    }

    [Fact]
    public void Booking_CreateCorporateVisitor_NormalizesPlate()
    {
        var booking = Booking.CreateCorporateVisitor(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            0m,
            " ka-01 ab 99 ");

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.VehicleNumber.Should().Be("KA-01 AB 99"); // uppercased; spaces retained by factory
        booking.IsCorporateStaged.Should().BeTrue();
    }

    [Fact]
    public void Booking_CreateMarketplace_IsNotCorporateStaged()
    {
        var booking = Booking.CreateMarketplace(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            PricingType.Hourly,
            VehicleType.Car,
            100m, 10m, 5m, 0m, 115m);

        booking.IsCorporateStaged.Should().BeFalse();
        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public void LicensePlate_Normalize_And_Matches()
    {
        LicensePlate.Normalize("  ka 01 ab ").Should().Be("KA01AB");
        LicensePlate.Normalize("   ").Should().BeNull();
        LicensePlate.Matches("KA-01 AB", "KA01AB").Should().BeTrue();
        LicensePlate.Matches("KA01AB", "MH12CD").Should().BeFalse();
        LicensePlate.ToMatchKey("KA-01 AB 1234").Should().Be("KA01AB1234");
    }

    [Fact]
    public void DynamicPricing_Disabled_ReturnsBaseRate()
    {
        var result = DynamicPricingCalculator.Calculate(
            baseRate: 100m,
            enabled: false,
            totalSpots: 10,
            availableSpots: 2,
            asOfUtc: DateTime.UtcNow);

        result.Applied.Should().BeFalse();
        result.EffectiveRate.Should().Be(100m);
    }

    [Fact]
    public void DynamicPricing_Enabled_AppliesMultiplier()
    {
        // Mid-weekday morning peak UTC (08:00)
        var asOf = new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc); // Wednesday
        var result = DynamicPricingCalculator.Calculate(
            baseRate: 100m,
            enabled: true,
            totalSpots: 10,
            availableSpots: 1,
            asOfUtc: asOf,
            timeZoneId: "UTC");

        result.Applied.Should().BeTrue();
        result.EffectiveRate.Should().BeGreaterThan(100m);
        result.IsPeakHour.Should().BeTrue();
        result.Multiplier.Should().BeGreaterThan(1m);
    }

    [Fact]
    public void BookingAvailability_OkAndFail()
    {
        BookingAvailabilityResult.Ok().IsAllowed.Should().BeTrue();
        var fail = BookingAvailabilityResult.Fail("full");
        fail.IsAllowed.Should().BeFalse();
        fail.ErrorMessage.Should().Be("full");
    }
}
