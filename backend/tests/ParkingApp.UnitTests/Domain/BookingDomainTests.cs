using FluentAssertions;
using Xunit;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Events;

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
        booking.DomainEvents.Should().ContainSingle(e => e is BookingConfirmedEvent);
    }

    [Fact]
    public void ExpireUnpaid_FromAwaitingPayment_SetsExpiredAndRaisesPaymentExpiredEvent()
    {
        var booking = new Booking
        {
            Status = BookingStatus.AwaitingPayment,
            UserId = Guid.NewGuid(),
            ParkingSpaceId = Guid.NewGuid(),
            BookingReference = "BK-PAY-1"
        };

        booking.ExpireUnpaid("qr expired");

        booking.Status.Should().Be(BookingStatus.Expired);
        var expired = booking.DomainEvents.Should().ContainSingle(e => e is BookingPaymentExpiredEvent)
            .Which.Should().BeOfType<BookingPaymentExpiredEvent>().Subject;
        expired.BookingId.Should().Be(booking.Id);
        expired.UserId.Should().Be(booking.UserId);
        expired.ParkingSpaceId.Should().Be(booking.ParkingSpaceId);
        expired.BookingReference.Should().Be("BK-PAY-1");
        expired.Reason.Should().Be("qr expired");
        booking.DomainEvents.Should().NotContain(e => e is BookingCancelledEvent);
    }

    [Fact]
    public void Confirm_FromExpired_Throws()
    {
        var booking = new Booking { Status = BookingStatus.Expired };

        var act = () => booking.Confirm();

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("Cannot confirm booking in Expired status");
    }

    [Fact]
    public void ExpireUnpaid_FromConfirmed_Throws()
    {
        var booking = new Booking { Status = BookingStatus.Confirmed };

        var act = () => booking.ExpireUnpaid("too late");

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("Cannot expire unpaid booking in Confirmed status");
    }

    [Fact]
    public void RecordExtensionFeePaid_SetsExtensionFeeTransactionId()
    {
        var booking = new Booking { Status = BookingStatus.AwaitingExtensionPayment };

        booking.RecordExtensionFeePaid("  pi_ext_1  ");

        booking.ExtensionFeeTransactionId.Should().Be("pi_ext_1");
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.Expired)]
    public void Confirm_WhenStatusIsNotPendingOrAwaitingPayment_ShouldThrowException(BookingStatus status)
    {
        // Arrange
        var booking = new Booking { Status = status };

        // Act
        var act = () => booking.Confirm();

        // Assert
        act.Should().Throw<BusinessRuleException>()
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
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCancelledEvent);
    }

    [Fact]
    public void Cancel_ClearsPendingExtensionFields()
    {
        var booking = new Booking
        {
            Status = BookingStatus.PendingExtension,
            PendingExtensionEndDateTime = DateTime.UtcNow.AddHours(2),
            PendingExtensionAmount = 25m,
            PreExtensionStatus = BookingStatus.InProgress
        };

        booking.Cancel("admin support");

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.PendingExtensionEndDateTime.Should().BeNull();
        booking.PendingExtensionAmount.Should().BeNull();
        booking.PreExtensionStatus.Should().BeNull();
        booking.HasPendingExtension.Should().BeFalse();
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
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCheckedInEvent);
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
        act.Should().Throw<BusinessRuleException>()
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
        act.Should().Throw<ValidationException>().WithMessage("*Invalid discount amount*");
    }

    [Fact]
    public void ApplyDiscount_WhenNegativeAmount_ShouldThrowValidationException()
    {
        var booking = new Booking { Status = BookingStatus.Pending };
        var act = () => booking.ApplyDiscount("CHEAT", -10);
        act.Should().Throw<ValidationException>().WithMessage("*Invalid discount amount*");
    }

    [Fact]
    public void Actions_OnInvalidStates_ShouldThrowExceptions()
    {
        var b1 = new Booking { Status = BookingStatus.Confirmed };
        Action a1 = () => b1.AwaitPayment();
        a1.Should().Throw<BusinessRuleException>();

        var b2 = new Booking { Status = BookingStatus.Confirmed };
        Action a2 = () => b2.Reject("reason");
        a2.Should().Throw<BusinessRuleException>();

        var b3 = new Booking { Status = BookingStatus.Completed };
        Action a3 = () => b3.Cancel("reason");
        a3.Should().Throw<BusinessRuleException>();

        var b4 = new Booking { Status = BookingStatus.Pending };
        Action a4 = () => b4.CheckIn();
        a4.Should().Throw<BusinessRuleException>();
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





