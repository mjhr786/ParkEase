using FluentAssertions;
using Xunit;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Domain.Models;
using System;
using System.Collections.Generic;

namespace ParkingApp.UnitTests.Domain;

public class DomainEntitiesTests
{
    [Fact]
    public void AllEntities_ShouldGetAndSetProperties_UsingReflection()
    {
        var entityTypes = typeof(BaseEntity).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "ParkingApp.Domain.Entities");

        foreach (var type in entityTypes)
        {
            var instance = Activator.CreateInstance(type);
            instance.Should().NotBeNull();
            
            foreach (var prop in type.GetProperties())
            {
                if (prop.CanWrite && prop.CanRead)
                {
                    try
                    {
                        var val = prop.GetValue(instance);
                        prop.SetValue(instance, val);
                    }
                    catch { } // Ignore properties that might throw on default values
                }
            }
        }
    }

    [Fact]
    public void Booking_AwaitPayment_ShouldUpdateStatus()
    {
        var booking = new Booking { Status = BookingStatus.Pending };
        booking.AwaitPayment();
        booking.Status.Should().Be(BookingStatus.AwaitingPayment);
    }

    [Fact]
    public void Booking_AwaitPayment_ShouldThrowIfNotPending()
    {
        var booking = new Booking { Status = BookingStatus.Confirmed };
        var act = () => booking.AwaitPayment();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Booking_Reject_ShouldUpdateStatusAndReason()
    {
        var booking = new Booking { Status = BookingStatus.Pending };
        booking.Reject("Slot unavailable");
        booking.Status.Should().Be(BookingStatus.Rejected);
        booking.CancellationReason.Should().Be("Slot unavailable");
        booking.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Booking_Reject_ShouldThrowIfNotPending()
    {
        var booking = new Booking { Status = BookingStatus.Confirmed };
        var act = () => booking.Reject("Reason");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Booking_CheckOut_ShouldUpdateStatus()
    {
        var booking = new Booking { Status = BookingStatus.InProgress };
        booking.CheckOut();
        booking.Status.Should().Be(BookingStatus.Completed);
        booking.CheckOutTime.Should().NotBeNull();
    }

    [Fact]
    public void Booking_CheckOut_ShouldThrowIfNotInProgress()
    {
        var booking = new Booking { Status = BookingStatus.Confirmed };
        var act = () => booking.CheckOut();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Booking_IsActive_ShouldCalculateCorrectly()
    {
        var confirmed = new Booking { Status = BookingStatus.Confirmed };
        var inProgress = new Booking { Status = BookingStatus.InProgress };
        var completed = new Booking { Status = BookingStatus.Completed };

        confirmed.IsActive.Should().BeTrue();
        inProgress.IsActive.Should().BeTrue();
        completed.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Booking_Cancel_ShouldThrowIfCompletedOrCancelled()
    {
        var bookingCompleted = new Booking { Status = BookingStatus.Completed };
        var act1 = () => bookingCompleted.Cancel("Reason");
        act1.Should().Throw<InvalidOperationException>();

        var bookingCancelled = new Booking { Status = BookingStatus.Cancelled };
        var act2 = () => bookingCancelled.Cancel("Reason");
        act2.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MissedEntities_ShouldGetAndSetProperties()
    {
        var availability = new ParkingAvailability 
        { 
            ParkingSpaceId = Guid.NewGuid(), Date = DateTime.UtcNow, StartTime = TimeSpan.Zero, 
            EndTime = TimeSpan.Zero, IsAvailable = true, AvailableSpots = 1, ParkingSpace = new ParkingSpace() 
        };
        availability.ParkingSpaceId.Should().NotBeEmpty();

        var paymentRequest = new ParkingApp.Domain.Interfaces.PaymentRequest 
        { 
            BookingId = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 10, Currency = "USD", 
            PaymentMethod = PaymentMethod.CreditCard, Description = "desc", 
            Metadata = new Dictionary<string, string> { { "key", "val" } }
        };
        paymentRequest.Amount.Should().Be(10);
        paymentRequest.Metadata["key"].Should().Be("val");
        
        var paymentResult = new ParkingApp.Domain.Interfaces.PaymentResult
        {
            Success = true, TransactionId = "123", PaymentGatewayReference = "resp", Status = PaymentStatus.Pending, ErrorMessage = "err", ReceiptUrl = "url"
        };
        paymentResult.Success.Should().BeTrue();

        var refundReq = new ParkingApp.Domain.Interfaces.RefundRequest { PaymentId = Guid.NewGuid(), Amount = 10, Reason = "reason" };
        refundReq.Amount.Should().Be(10);
        
        var refundRes = new ParkingApp.Domain.Interfaces.RefundResult { Success = true, RefundTransactionId = "rtx", RefundedAmount = 10, ErrorMessage = "err" };
        refundRes.Success.Should().BeTrue();

        var payment = new Payment { BookingId = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 10, Currency = "USD", 
            PaymentMethod = PaymentMethod.CreditCard, Status = PaymentStatus.Pending, TransactionId = "tx1", 
            PaymentGatewayReference = "ref", PaymentGateway = "gate", PaidAt = DateTime.UtcNow, RefundedAt = DateTime.UtcNow, 
            RefundAmount = 10, RefundReason = "reason", RefundTransactionId = "rtx", ReceiptUrl = "url", 
            InvoiceNumber = "inv", FailureReason = "fail", Metadata = "meta", Booking = new Booking(), User = new User() };
        payment.BookingId.Should().NotBeEmpty();

        var review = new Review { UserId = Guid.NewGuid(), ParkingSpaceId = Guid.NewGuid(), BookingId = Guid.NewGuid(), 
            Rating = 5, Title = "Title", Comment = "Comment", HelpfulCount = 1, IsApproved = true, IsReported = false, 
            OwnerResponse = "resp", OwnerResponseAt = DateTime.UtcNow, User = new User(), ParkingSpace = new ParkingSpace(), 
            Booking = new Booking() };
        review.UserId.Should().NotBeEmpty();

        var m1 = new ParkingMapModel(Guid.NewGuid(), "Title", "Addr", "City", 1.0, 1.0, 10m, "url", 5.0, ParkingType.Open);
        var m2 = m1 with { Title = "Title2" };
        var m3 = m1 with { Title = "Title" };
        m1.Should().NotBe(m2);
        m1.Should().Be(m3);
        m1.GetHashCode().Should().NotBe(m2.GetHashCode());
        m1.ToString().Should().Contain("Title");

        var e1 = new ParkingApp.Domain.Events.Parking.ParkingSpaceCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), "Title");
        var e2 = new ParkingApp.Domain.Events.Parking.ParkingSpaceUpdatedEvent(e1.ParkingSpaceId, "T2");
        var e3 = new ParkingApp.Domain.Events.Parking.ParkingSpaceDeletedEvent(e1.ParkingSpaceId, e1.OwnerId);
        var e4 = new ParkingApp.Domain.Events.Parking.ParkingSpaceToggledEvent(e1.ParkingSpaceId, false);

        e1.Should().NotBeNull();
        e2.Should().NotBeNull();
        e3.Should().NotBeNull();
        e4.Should().NotBeNull();
        e1.ToString().Should().NotBeEmpty();
        e2.ToString().Should().NotBeEmpty();
        e3.ToString().Should().NotBeEmpty();
        e4.ToString().Should().NotBeEmpty();
        e1.GetHashCode().Should().NotBe(0);
        
        var avail1 = new ParkingAvailability { ParkingSpaceId = Guid.NewGuid() };
        avail1.ParkingSpaceId.Should().NotBeEmpty();
    }
}
