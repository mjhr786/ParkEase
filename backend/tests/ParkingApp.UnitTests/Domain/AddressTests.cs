using FluentAssertions;
using Xunit;
using ParkingApp.Domain.ValueObjects;
using System;

namespace ParkingApp.UnitTests.Domain;

public class AddressTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateInstance()
    {
        var address = new Address("123 Main St", "City", "State", "Country", "12345", 40.7128, -74.0060);
        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("City");
        address.State.Should().Be("State");
        address.Country.Should().Be("Country");
        address.PostalCode.Should().Be("12345");
        address.Latitude.Should().Be(40.7128);
        address.Longitude.Should().Be(-74.0060);
    }

    [Fact]
    public void Constructor_WithNullPostalCode_ShouldSetEmptyString()
    {
        var address = new Address("123 Main St", "City", "State", "Country", null, 40.7128, -74.0060);
        address.PostalCode.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "City", "State", "Country", 40, -70, "Street cannot be empty*")]
    [InlineData("Street", "", "State", "Country", 40, -70, "City cannot be empty*")]
    [InlineData("Street", "City", "", "Country", 40, -70, "State cannot be empty*")]
    [InlineData("Street", "City", "State", "", 40, -70, "Country cannot be empty*")]
    [InlineData("Street", "City", "State", "Country", -91, -70, "Latitude must be between*")]
    [InlineData("Street", "City", "State", "Country", 91, -70, "Latitude must be between*")]
    [InlineData("Street", "City", "State", "Country", 40, -181, "Longitude must be between*")]
    [InlineData("Street", "City", "State", "Country", 40, 181, "Longitude must be between*")]
    public void Constructor_WithInvalidData_ShouldThrowArgumentException(string street, string city, string state, string country, double lat, double lon, string expectedMessage)
    {
        var act = () => new Address(street, city, state, country, "12345", lat, lon);
        act.Should().Throw<ArgumentException>().WithMessage(expectedMessage);
    }

    [Fact]
    public void FullAddress_ShouldReturnFormattedString()
    {
        var address = new Address("123 Main St", "City", "State", "Country", "12345", 40, -70);
        address.FullAddress.Should().Be("123 Main St, City, State, Country 12345");
        address.ToString().Should().Be(address.FullAddress);
    }

    [Fact]
    public void DistanceToKm_ShouldCalculateCorrectly()
    {
        // New York City
        var ny = new Address("123", "NY", "NY", "USA", "10001", 40.7128, -74.0060);
        // Los Angeles
        var la = new Address("456", "LA", "CA", "USA", "90001", 34.0522, -118.2437);

        var distance = ny.DistanceToKm(la);

        // Distance is approx 3936 km
        distance.Should().BeApproximately(3935, 10);
    }
}
