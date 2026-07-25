using FluentAssertions;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Marketplace.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class BookingExtensionEventTests
{
    private static Booking ConfirmedBooking()
    {
        var booking = Booking.CreateMarketplace(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            PricingType.Hourly,
            VehicleType.Car,
            100, 0, 0, 0, 100);
        booking.Confirm();
        booking.ClearDomainEvents();
        return booking;
    }

    [Fact]
    public void RequestExtension_Raises_ExtensionRequestedEvent()
    {
        var booking = ConfirmedBooking();
        var newEnd = booking.EndDateTime.AddHours(2);
        booking.RequestExtension(newEnd, 50m);

        booking.DomainEvents.Should().ContainSingle(e => e is BookingExtensionRequestedEvent);
        var ev = booking.DomainEvents.OfType<BookingExtensionRequestedEvent>().Single();
        ev.ExtraAmount.Should().Be(50m);
        ev.NewEndUtc.Should().Be(newEnd);
    }

    [Fact]
    public void ApproveExtension_Raises_ApprovedEvent_WithVendor()
    {
        var booking = ConfirmedBooking();
        booking.RequestExtension(booking.EndDateTime.AddHours(1), 25m);
        booking.ClearDomainEvents();
        var vendorId = Guid.NewGuid();

        booking.ApproveExtension(vendorId);

        var ev = booking.DomainEvents.OfType<BookingExtensionApprovedEvent>().Single();
        ev.RequiresPayment.Should().BeTrue();
        ev.VendorUserId.Should().Be(vendorId);
        ev.ExtraAmount.Should().Be(25m);
    }

    [Fact]
    public void ConfirmExtension_Raises_ConfirmedEvent()
    {
        var booking = ConfirmedBooking();
        var newEnd = booking.EndDateTime.AddHours(2);
        booking.RequestExtension(newEnd, 0m);
        booking.ClearDomainEvents();

        booking.ConfirmExtension();

        var ev = booking.DomainEvents.OfType<BookingExtensionConfirmedEvent>().Single();
        ev.NewEndUtc.Should().Be(newEnd);
        ev.ExtraAmount.Should().Be(0m);
        booking.EndDateTime.Should().Be(newEnd);
    }

    [Fact]
    public void RejectExtension_Raises_RejectedEvent()
    {
        var booking = ConfirmedBooking();
        booking.RequestExtension(booking.EndDateTime.AddHours(1), 10m);
        booking.ClearDomainEvents();
        var vendorId = Guid.NewGuid();

        booking.RejectExtension("No availability", vendorId);

        var ev = booking.DomainEvents.OfType<BookingExtensionRejectedEvent>().Single();
        ev.Reason.Should().Be("No availability");
        ev.VendorUserId.Should().Be(vendorId);
        booking.Status.Should().Be(BookingStatus.Confirmed);
    }
}





