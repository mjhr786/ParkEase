using FluentAssertions;
using ParkingApp.Marketplace.Application.Services;

namespace ParkingApp.UnitTests.Services;

public class IndianMobileNormalizerTests
{
    [Theory]
    [InlineData("+91 98765 43210", "9876543210")]
    [InlineData("919876543210", "9876543210")]
    [InlineData("09876543210", "9876543210")]
    [InlineData("9876543210", "9876543210")]
    public void TryNormalize_ValidIndianMobile_ReturnsTenDigits(string input, string expected)
    {
        IndianMobileNormalizer.TryNormalize(input, out var normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567890")]
    [InlineData("5876543210")]
    [InlineData("")]
    [InlineData(null)]
    public void TryNormalize_Invalid_ReturnsFalse(string? input)
    {
        IndianMobileNormalizer.TryNormalize(input, out var normalized).Should().BeFalse();
        normalized.Should().BeEmpty();
    }
}
