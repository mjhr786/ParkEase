using FluentAssertions;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Marketplace.Domain.ValueObjects;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class LicensePlateAndLprTests
{
    [Theory]
    [InlineData(" ka 01 ab 1234 ", "KA01AB1234")]
    [InlineData("abc-12", "ABC-12")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void LicensePlate_Normalize_TrimsAndUppercases(string? input, string? expected)
    {
        LicensePlate.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("KA-01-AB-1234", "KA01AB1234")]
    [InlineData("ka 01 ab 1234", "KA01AB1234")]
    [InlineData("KA01AB1234", "KA01AB1234")]
    public void LicensePlate_ToMatchKey_StripsNonAlphanumeric(string input, string expected)
    {
        LicensePlate.ToMatchKey(input).Should().Be(expected);
    }

    [Fact]
    public void LicensePlate_Matches_FuzzyHyphenVariants()
    {
        LicensePlate.Matches("KA-01-AB-1234", "KA01AB1234").Should().BeTrue();
        LicensePlate.Matches("KA 01 AB 1234", "KA-01AB1234").Should().BeTrue();
        LicensePlate.Matches("KA01AB1234", "XX99YY0000").Should().BeFalse();
    }

    [Fact]
    public void CheckIn_WithExplicitTimestamp_SetsCheckInTimeAndEvent()
    {
        var start = DateTime.UtcNow.AddMinutes(20);
        var end = start.AddHours(2);
        var booking = new Booking
        {
            Status = BookingStatus.Confirmed,
            StartDateTime = start,
            EndDateTime = end
        };
        var at = DateTime.UtcNow;

        booking.CheckIn(at);

        booking.Status.Should().Be(BookingStatus.InProgress);
        booking.CheckInTime.Should().Be(at);
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCheckedInEvent);
    }

    [Fact]
    public void CheckIn_AfterEndTime_Throws()
    {
        var booking = new Booking
        {
            Status = BookingStatus.Confirmed,
            StartDateTime = DateTime.UtcNow.AddHours(-3),
            EndDateTime = DateTime.UtcNow.AddHours(-1)
        };

        var act = () => booking.CheckIn(DateTime.UtcNow);

        act.Should().Throw<BusinessRuleException>()
            .Where(e => e.RuleName == "Booking.CheckInWindow");
    }

    [Fact]
    public void CheckOut_WithExplicitTimestamp_CompletesBooking()
    {
        var booking = new Booking
        {
            Status = BookingStatus.InProgress,
            StartDateTime = DateTime.UtcNow.AddHours(-1),
            EndDateTime = DateTime.UtcNow.AddHours(1)
        };
        var at = DateTime.UtcNow;

        booking.CheckOut(at);

        booking.Status.Should().Be(BookingStatus.Completed);
        booking.CheckOutTime.Should().Be(at);
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCheckedOutEvent);
    }

    [Fact]
    public void LprAccessAttempt_CreateGranted_SetsDecision()
    {
        var spaceId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var at = DateTime.UtcNow;

        var attempt = LprAccessAttempt.CreateGranted(
            spaceId,
            "ka01ab1234",
            "KA01AB1234",
            LprDirection.Entry,
            at,
            bookingId,
            LprAccessSources.Simulator,
            "admin:1");

        attempt.Decision.Should().Be(LprAccessDecision.Granted);
        attempt.BookingId.Should().Be(bookingId);
        attempt.DenialReason.Should().BeNull();
        attempt.Source.Should().Be(LprAccessSources.Simulator);
    }

    [Fact]
    public void LprAccessAttempt_CreateDenied_RequiresReason()
    {
        var act = () => LprAccessAttempt.CreateDenied(
            Guid.NewGuid(),
            "X",
            "X",
            LprDirection.Entry,
            DateTime.UtcNow,
            "",
            LprAccessSources.Iot);

        act.Should().Throw<ValidationException>();
    }
}
