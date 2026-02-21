using FluentAssertions;
using Xunit;
using ParkingApp.Domain.ValueObjects;
using System;

namespace ParkingApp.UnitTests;

public class DomainLogicTests
{
    [Fact]
    public void Money_Operations_ShouldBeCorrect()
    {
        // Arithmetic
        var m1 = new Money(100);
        var m2 = new Money(50);
        
        (m1 + m2).Amount.Should().Be(150);
        (m1 - m2).Amount.Should().Be(50);
        (m1 * 2).Amount.Should().Be(200);

        // Comparison
        (m1 > m2).Should().BeTrue();
        (m1 < m2).Should().BeFalse();
        (m1 >= m2).Should().BeTrue();
        
        // Validation
        FluentActions.Invoking(() => new Money(-10))
            .Should().Throw<ArgumentException>();
            
        // Different Currencies
        var usd = new Money(10, "USD");
        FluentActions.Invoking(() => m1 + usd)
            .Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("TEST@EXAMPLE.COM", true)] // Normalized to lower
    [InlineData("invalid-email", false)]
    [InlineData("", false)]
    public void Email_Validation_ShouldCorrectlyVerifyFormat(string emailValue, bool expectedValid)
    {
        if (expectedValid)
        {
            var email = new Email(emailValue);
            email.Value.Should().Be(emailValue.ToLowerInvariant());
        }
        else
        {
            FluentActions.Invoking(() => new Email(emailValue))
                .Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void Address_DistanceCalculation_ShouldBeAccurate()
    {
        // Mumbai to Pune approx 120-150km
        var mumbai = new Address("Marine Drive", "Mumbai", "MH", "IN", "400001", 18.9220, 72.8347);
        var pune = new Address("FC Road", "Pune", "MH", "IN", "411001", 18.5204, 73.8567);

        var distance = mumbai.DistanceToKm(pune);
        
        // Pune is roughly 120km away as the crow flies
        distance.Should().BeInRange(110, 130);
    }

    [Fact]
    public void Address_InValidCoordinates_ShouldThrow()
    {
        FluentActions.Invoking(() => new Address("A", "B", "C", "D", "E", 100, 0))
            .Should().Throw<ArgumentException>().WithMessage("*Latitude*");

        FluentActions.Invoking(() => new Address("A", "B", "C", "D", "E", 0, 200))
            .Should().Throw<ArgumentException>().WithMessage("*Longitude*");
    }
}
