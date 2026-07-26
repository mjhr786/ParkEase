using FluentAssertions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class P2PDrivewayListingTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static ParkingSpace CreateLot(int spots = 5) =>
        ParkingSpace.CreateForVendor(
            OwnerId, "Home Driveway", "Private spot", "12 Oak St", "Bengaluru", "KA", "IN", "560001",
            12.9, 77.6, ParkingType.Open, spots, 30, 200, 1000, 3500);

    [Fact]
    public void SetListingCategory_Residential_DefaultsInstantBookAndDrivewayAmenity()
    {
        var lot = CreateLot();
        lot.ClearDomainEvents();

        lot.SetListingCategory(ListingCategory.Residential);

        lot.ListingCategory.Should().Be(ListingCategory.Residential);
        lot.InstantBook.Should().BeTrue();
        lot.Amenities.Should().Contain("Driveway");
    }

    [Fact]
    public void SetListingCategory_Residential_CapsSpotsAt10()
    {
        var lot = CreateLot(spots: 20);
        lot.SetListingCategory(ListingCategory.Residential);
        lot.TotalSpots.Should().Be(10);
    }

    [Fact]
    public void SetListingCategory_Residential_RespectsExplicitInstantBookFalse()
    {
        var lot = CreateLot();
        lot.SetListingCategory(ListingCategory.Residential, instantBook: false);
        lot.InstantBook.Should().BeFalse();
    }

    [Fact]
    public void UpdateDetails_Residential_RejectsSpotsOver10()
    {
        var lot = CreateLot(1);
        lot.SetListingCategory(ListingCategory.Residential);

        var act = () => lot.UpdateDetails(totalSpots: 11);

        act.Should().Throw<ParkingApp.BuildingBlocks.Exceptions.ValidationException>();
    }

    [Fact]
    public void CreateForVendor_DefaultsCommercialNotInstantBook()
    {
        var lot = CreateLot();
        lot.ListingCategory.Should().Be(ListingCategory.Commercial);
        lot.InstantBook.Should().BeFalse();
    }
}
