using FluentAssertions;
using Xunit;
using ParkingApp.Application.Mappings;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using NetTopologySuite.Geometries;

namespace ParkingApp.UnitTests;

public class MappingTests
{
    [Fact]
    public void ToDto_WithNullOptionalCollections_ShouldReturnEmptyListsInDto()
    {
        // Arrange
        var parking = new ParkingSpace
        {
            Id = Guid.NewGuid(),
            Amenities = null,
            AllowedVehicleTypes = null,
            ImageUrls = null
        };

        // Act
        var dto = parking.ToDto();

        // Assert
        dto.Amenities.Should().BeEmpty();
        dto.AllowedVehicleTypes.Should().BeEmpty();
        dto.ImageUrls.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_WithCommaSeparatedValues_ShouldParseCorrectly()
    {
        // Arrange
        var parking = new ParkingSpace
        {
            Id = Guid.NewGuid(),
            Amenities = "CCTV, Wi-Fi, Water",
            AllowedVehicleTypes = "Car,Motorcycle"
        };

        // Act
        var dto = parking.ToDto();

        // Assert
        dto.Amenities.Should().HaveCount(3).And.ContainInOrder("CCTV", "Wi-Fi", "Water");
        dto.AllowedVehicleTypes.Should().HaveCount(2).And.Contain(VehicleType.Car).And.Contain(VehicleType.Motorcycle);
    }

    [Fact]
    public void ToEntity_FromDto_ShouldSetGeoLocationCorrectly()
    {
        // Arrange
        var dto = new CreateParkingSpaceDto(
            "Test", "Desc", "Addr", "City", "ST", "IN", "123",
            12.9716, 77.5946, ParkingType.Open, 10, 50, 400, 2000, 7000, null, null);

        // Act
        var entity = dto.ToEntity(Guid.NewGuid());

        // Assert
        entity.Latitude.Should().Be(12.9716);
        entity.Longitude.Should().Be(77.5946);
        entity.Location.Should().NotBeNull();
        entity.Location.X.Should().Be(77.5946); // Longitude
        entity.Location.Y.Should().Be(12.9716); // Latitude
        entity.Location.SRID.Should().Be(4326);
    }

    [Fact]
    public void ToDtoWithReservations_ShouldFilterOldAndIrrelevantBookings()
    {
        // Arrange
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId };
        var now = DateTime.UtcNow;
        var bookings = new List<Booking>
        {
            new Booking { ParkingSpaceId = parkingId, Status = BookingStatus.Confirmed, StartDateTime = now.AddHours(1), EndDateTime = now.AddHours(2) }, // OK
            new Booking { ParkingSpaceId = parkingId, Status = BookingStatus.Completed, StartDateTime = now.AddHours(-2), EndDateTime = now.AddHours(-1) }, // SKIP (Completed)
            new Booking { ParkingSpaceId = parkingId, Status = BookingStatus.Confirmed, StartDateTime = now.AddHours(-3), EndDateTime = now.AddHours(-2) }  // SKIP (Old)
        };

        // Act
        var dto = parking.ToDtoWithReservations(bookings);

        // Assert
        dto.ActiveReservations.Should().HaveCount(1);
        dto.ActiveReservations!.First().StartDateTime.Should().Be(bookings[0].StartDateTime);
    }
}
