using FluentAssertions;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Events.Parking;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class ParkingSpaceFactoryTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    [Fact]
    public void CreateForVendor_RaisesCreatedEvent()
    {
        var parking = ParkingSpace.CreateForVendor(
            OwnerId, "Lot A", "Desc", "Addr", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 5, 40, 300, 1500, 5000);

        parking.OwnerId.Should().Be(OwnerId);
        parking.OwnershipType.Should().Be(ParkingSpaceOwnershipType.IndividualVendor);
        parking.IsCorporateOnly.Should().BeFalse();
        parking.TotalSpots.Should().Be(5);
        parking.AvailableSpots.Should().Be(5);
        parking.Location.Should().NotBeNull();
        parking.DomainEvents.Should().ContainSingle(e => e is ParkingSpaceCreatedEvent);
    }

    [Fact]
    public void CreateForCompany_IsVerifiedCorporateOwned()
    {
        var parking = ParkingSpace.CreateForCompany(
            OwnerId, CompanyId, "HQ Lot", "Desc", "Addr", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Covered, 20, 0, 0, 0, 0);

        parking.CompanyOwnerId.Should().Be(CompanyId);
        parking.OwnershipType.Should().Be(ParkingSpaceOwnershipType.CompanyOwned);
        parking.IsCorporateOnly.Should().BeTrue();
        parking.IsVerified.Should().BeTrue();
        parking.DomainEvents.Should().ContainSingle(e => e is ParkingSpaceCreatedEvent);
    }

    [Fact]
    public void CreateForVendor_InvalidSpots_Throws()
    {
        var act = () => ParkingSpace.CreateForVendor(
            OwnerId, "Lot", "D", "A", "C", "S", "IN", "1",
            0, 0, ParkingType.Open, 0, 1, 1, 1, 1);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ToggleActive_RaisesToggledEvent()
    {
        var parking = ParkingSpace.CreateForVendor(
            OwnerId, "Lot", "D", "A", "C", "S", "IN", "1",
            0, 0, ParkingType.Open, 2, 10, 10, 10, 10);
        parking.ClearDomainEvents();

        parking.ToggleActive();

        parking.IsActive.Should().BeFalse();
        var toggled = parking.DomainEvents.OfType<ParkingSpaceToggledEvent>().Should().ContainSingle().Subject;
        toggled.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Retire_SoftDeletes_AndRaisesDeletedEvent()
    {
        var parking = ParkingSpace.CreateForVendor(
            OwnerId, "Lot", "D", "A", "C", "S", "IN", "1",
            0, 0, ParkingType.Open, 2, 10, 10, 10, 10);
        parking.ClearDomainEvents();

        parking.Retire(OwnerId);

        parking.IsActive.Should().BeFalse();
        parking.IsDeleted.Should().BeTrue();
        parking.DomainEvents.Should().ContainSingle(e => e is ParkingSpaceDeletedEvent);
    }

    [Fact]
    public void UpdateDetails_RaisesUpdatedEvent_AndSyncsLocation()
    {
        var parking = ParkingSpace.CreateForVendor(
            OwnerId, "Lot", "D", "A", "C", "S", "IN", "1",
            1, 1, ParkingType.Open, 2, 10, 10, 10, 10);
        parking.ClearDomainEvents();

        parking.UpdateDetails(title: "New Title", latitude: 13.0, longitude: 77.0);

        parking.Title.Should().Be("New Title");
        parking.Latitude.Should().Be(13.0);
        parking.Longitude.Should().Be(77.0);
        parking.Location!.X.Should().Be(77.0);
        parking.Location.Y.Should().Be(13.0);
        parking.DomainEvents.Should().ContainSingle(e => e is ParkingSpaceUpdatedEvent);
    }

    [Fact]
    public void RecordNewReview_UpdatesAverage()
    {
        var parking = ParkingSpace.CreateForVendor(
            OwnerId, "Lot", "D", "A", "C", "S", "IN", "1",
            0, 0, ParkingType.Open, 2, 10, 10, 10, 10);

        parking.RecordNewReview(4);
        parking.RecordNewReview(2);

        parking.TotalReviews.Should().Be(2);
        parking.AverageRating.Should().Be(3);
    }

    [Fact]
    public void AppendImageUrls_Deduplicates()
    {
        var parking = ParkingSpace.CreateForVendor(
            OwnerId, "Lot", "D", "A", "C", "S", "IN", "1",
            0, 0, ParkingType.Open, 2, 10, 10, 10, 10,
            imageUrls: new[] { "a.jpg" });
        parking.ClearDomainEvents();

        parking.AppendImageUrls(new[] { "a.jpg", "b.jpg" });

        parking.ImageUrls.Should().Be("a.jpg,b.jpg");
    }
}
