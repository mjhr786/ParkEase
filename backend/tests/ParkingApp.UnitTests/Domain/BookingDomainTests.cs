using FluentAssertions;
using Xunit;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests.Domain;

public class BookingDomainTests
{
    [Fact]
    public void Confirm_WhenStatusIsPending_ShouldSetStatusToConfirmed()
    {
        // Arrange
        var booking = new Booking { Status = BookingStatus.Pending };

        // Act
        booking.Confirm();

        // Assert
        booking.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Completed)]
    public void Confirm_WhenStatusIsNotPendingOrAwaitingPayment_ShouldThrowException(BookingStatus status)
    {
        // Arrange
        var booking = new Booking { Status = status };

        // Act
        var act = () => booking.Confirm();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Cannot confirm booking in {status} status");
    }

    [Fact]
    public void Cancel_WhenValid_ShouldSetStatusToCancelledAndRecordReason()
    {
        // Arrange
        var booking = new Booking { Status = BookingStatus.Confirmed };
        var reason = "User changed mind";

        // Act
        booking.Cancel(reason);

        // Assert
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancellationReason.Should().Be(reason);
        booking.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void CheckIn_WhenWithinOneHourOfStartTime_ShouldSetStatusToInProgress()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddMinutes(30);
        var booking = new Booking 
        { 
            Status = BookingStatus.Confirmed,
            StartDateTime = startTime
        };

        // Act
        booking.CheckIn();

        // Assert
        booking.Status.Should().Be(BookingStatus.InProgress);
        booking.CheckInTime.Should().NotBeNull();
    }

    [Fact]
    public void CheckIn_WhenTooEarly_ShouldThrowException()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(2); // More than 1 hour away
        var booking = new Booking 
        { 
            Status = BookingStatus.Confirmed,
            StartDateTime = startTime
        };

        // Act
        var act = () => booking.CheckIn();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Check-in is only allowed within 1 hour before start time");
    }

    [Fact]
    public void ApplyDiscount_WhenValid_ShouldUpdateTotals()
    {
        // Arrange
        var booking = new Booking 
        { 
            Status = BookingStatus.Pending,
            BaseAmount = 100,
            TaxAmount = 10,
            ServiceFee = 5,
            TotalAmount = 115
        };

        // Act
        booking.ApplyDiscount("SAVE10", 10);

        // Assert
        booking.DiscountCode.Should().Be("SAVE10");
        booking.DiscountAmount.Should().Be(10);
        booking.TotalAmount.Should().Be(105); // 100 + 10 + 5 - 10
    }

    [Fact]
    public void ApplyDiscount_WhenInvalidAmount_ShouldThrowArgumentException()
    {
        // Arrange
        var booking = new Booking 
        { 
            Status = BookingStatus.Pending,
            BaseAmount = 100 
        };

        // Act & Assert
        var act = () => booking.ApplyDiscount("CHEAT", 200);
        act.Should().Throw<ArgumentException>().WithMessage("Invalid discount amount");
    }
}
