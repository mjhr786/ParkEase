using FluentAssertions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class AccessPassTests
{
    [Fact]
    public void Confirm_IssuesAccessPassToken()
    {
        var booking = new Booking
        {
            Status = BookingStatus.Pending,
            StartDateTime = DateTime.UtcNow.AddHours(-1),
            EndDateTime = DateTime.UtcNow.AddHours(2),
            BookingReference = "BKTEST01"
        };

        booking.Confirm();

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.QRCode.Should().NotBeNullOrWhiteSpace();
        booking.QRCode.Should().StartWith("PE-");
    }

    [Fact]
    public void EnsureAccessPass_IsIdempotent()
    {
        var booking = new Booking
        {
            Status = BookingStatus.Confirmed,
            BookingReference = "BKTEST02"
        };

        booking.EnsureAccessPass().Should().BeTrue();
        var first = booking.QRCode;
        booking.EnsureAccessPass().Should().BeFalse();
        booking.QRCode.Should().Be(first);
    }

    [Fact]
    public void IsAccessPassValidAt_True_InWindow_Confirmed()
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var booking = new Booking
        {
            Status = BookingStatus.Confirmed,
            StartDateTime = now.AddHours(-1),
            EndDateTime = now.AddHours(2),
            QRCode = "PE-TEST-TOKEN"
        };

        booking.IsAccessPassValidAt(now).Should().BeTrue();
    }

    [Fact]
    public void IsAccessPassValidAt_False_WhenExpired()
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var booking = new Booking
        {
            Status = BookingStatus.Confirmed,
            StartDateTime = now.AddHours(-3),
            EndDateTime = now.AddHours(-1),
            QRCode = "PE-TEST-TOKEN"
        };

        booking.IsAccessPassValidAt(now).Should().BeFalse();
    }

    [Fact]
    public void IsAccessPassValidAt_False_WhenTooEarly()
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var booking = new Booking
        {
            Status = BookingStatus.Confirmed,
            StartDateTime = now.AddHours(3),
            EndDateTime = now.AddHours(5),
            QRCode = "PE-TEST-TOKEN"
        };

        booking.IsAccessPassValidAt(now).Should().BeFalse();
    }

    [Fact]
    public void CreateCorporateVisitor_IssuesAccessPass()
    {
        var booking = Booking.CreateCorporateVisitor(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(4),
            0m,
            visitorLicensePlate: "KA01AB1234");

        booking.QRCode.Should().NotBeNullOrWhiteSpace();
        booking.IsAccessPassValidAt(DateTime.UtcNow.AddMinutes(5)).Should().BeTrue();
    }
}
