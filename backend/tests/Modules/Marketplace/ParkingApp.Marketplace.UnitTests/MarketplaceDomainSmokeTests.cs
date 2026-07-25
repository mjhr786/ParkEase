using FluentAssertions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.UnitTests;

public class MarketplaceDomainSmokeTests
{
    [Fact]
    public void ParkingSpace_CreateForVendor_IsActive()
    {
        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(),
            "Lot A",
            "Desc",
            "1 Main",
            "City",
            "ST",
            "Country",
            "00000",
            12.0,
            77.0,
            ParkingType.Open,
            10,
            50,
            200,
            1000,
            3000);

        space.IsActive.Should().BeTrue();
        space.Title.Should().Be("Lot A");
        space.TotalSpots.Should().Be(10);
    }
}
