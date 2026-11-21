using FluentAssertions;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Infrastructure.Services;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class PaymentCaptureMatcherTests
{
    [Fact]
    public void Matches_WhenAmountCurrencyBookingAndPurposeAlign_ReturnsTrue()
    {
        var bookingId = Guid.NewGuid();
        var expected = new PaymentCaptureCheck(bookingId, 10.00m, "INR", "booking");
        var metadata = new Dictionary<string, string>
        {
            ["bookingId"] = bookingId.ToString(),
            ["purpose"] = "booking"
        };

        PaymentCaptureMatcher.Matches(1000, "inr", metadata, expected).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenBookingIdDiffers_ReturnsFalse()
    {
        var expected = new PaymentCaptureCheck(Guid.NewGuid(), 10.00m, "INR", "booking");
        var metadata = new Dictionary<string, string>
        {
            ["bookingId"] = Guid.NewGuid().ToString(),
            ["purpose"] = "booking"
        };

        PaymentCaptureMatcher.Matches(1000, "INR", metadata, expected).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenPurposeDiffers_ReturnsFalse()
    {
        var bookingId = Guid.NewGuid();
        var expected = new PaymentCaptureCheck(bookingId, 10.00m, "INR", "booking");
        var metadata = new Dictionary<string, string>
        {
            ["bookingId"] = bookingId.ToString(),
            ["purpose"] = "extension"
        };

        PaymentCaptureMatcher.Matches(1000, "INR", metadata, expected).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenCurrencyDiffers_ReturnsFalse()
    {
        var bookingId = Guid.NewGuid();
        var expected = new PaymentCaptureCheck(bookingId, 10.00m, "INR", "booking");
        var metadata = new Dictionary<string, string>
        {
            ["bookingId"] = bookingId.ToString(),
            ["purpose"] = "booking"
        };

        PaymentCaptureMatcher.Matches(1000, "USD", metadata, expected).Should().BeFalse();
    }

    [Fact]
    public void ToMinorUnits_RoundsAwayFromZero()
    {
        PaymentCaptureMatcher.ToMinorUnits(10.005m).Should().Be(1001);
        PaymentCaptureMatcher.ToMinorUnits(10.004m).Should().Be(1000);
    }

    [Fact]
    public void Matches_UsesAwayFromZeroMinorUnits()
    {
        var bookingId = Guid.NewGuid();
        var expected = new PaymentCaptureCheck(bookingId, 10.005m, "INR", "booking");
        var metadata = new Dictionary<string, string>
        {
            ["bookingId"] = bookingId.ToString(),
            ["purpose"] = "booking"
        };

        PaymentCaptureMatcher.Matches(1001, "INR", metadata, expected).Should().BeTrue();
        PaymentCaptureMatcher.Matches(1000, "INR", metadata, expected).Should().BeFalse();
    }

    [Fact]
    public void Matches_FailsClosed_WhenExpectedIsInvalidOrMetadataMissing()
    {
        var bookingId = Guid.NewGuid();
        var metadata = new Dictionary<string, string>
        {
            ["bookingId"] = bookingId.ToString(),
            ["purpose"] = "booking"
        };

        PaymentCaptureMatcher.Matches(1000, "INR", metadata, new PaymentCaptureCheck(Guid.Empty, 10m, "INR", "booking")).Should().BeFalse();
        PaymentCaptureMatcher.Matches(1000, "INR", metadata, new PaymentCaptureCheck(bookingId, 0m, "INR", "booking")).Should().BeFalse();
        PaymentCaptureMatcher.Matches(1000, "INR", metadata, new PaymentCaptureCheck(bookingId, 10m, " ", "booking")).Should().BeFalse();
        PaymentCaptureMatcher.Matches(1000, "INR", null, new PaymentCaptureCheck(bookingId, 10m, "INR", "booking")).Should().BeFalse();
    }
}
