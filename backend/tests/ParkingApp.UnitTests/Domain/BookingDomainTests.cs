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

    [Fact]
    public void ApplyDiscount_WhenNegativeAmount_ShouldThrowArgumentException()
    {
        var booking = new Booking { Status = BookingStatus.Pending };
        var act = () => booking.ApplyDiscount("CHEAT", -10);
        act.Should().Throw<ArgumentException>().WithMessage("Invalid discount amount");
    }

    [Fact]
    public void Actions_OnInvalidStates_ShouldThrowExceptions()
    {
        var b1 = new Booking { Status = BookingStatus.Confirmed };
        Action a1 = () => b1.AwaitPayment();
        a1.Should().Throw<InvalidOperationException>();

        var b2 = new Booking { Status = BookingStatus.Confirmed };
        Action a2 = () => b2.Reject("reason");
        a2.Should().Throw<InvalidOperationException>();

        var b3 = new Booking { Status = BookingStatus.Completed };
        Action a3 = () => b3.Cancel("reason");
        a3.Should().Throw<InvalidOperationException>();

        var b4 = new Booking { Status = BookingStatus.Pending };
        Action a4 = () => b4.CheckIn();
        a4.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Actions_OnValidStates_ShouldSucceed()
    {
        var b1 = new Booking { Status = BookingStatus.Pending };
        b1.Reject("reason");
        b1.Status.Should().Be(BookingStatus.Rejected);

        var b2 = new Booking { Status = BookingStatus.Confirmed };
        b2.Cancel("reason");
        b2.Status.Should().Be(BookingStatus.Cancelled);

        var b3 = new Booking { Status = BookingStatus.Confirmed, StartDateTime = DateTime.UtcNow.AddMinutes(30) };
        b3.CheckIn();
        b3.Status.Should().Be(BookingStatus.InProgress);

        var b4 = new Booking { Status = BookingStatus.InProgress };
        b4.CheckOut();
        b4.Status.Should().Be(BookingStatus.Completed);
    }
}
