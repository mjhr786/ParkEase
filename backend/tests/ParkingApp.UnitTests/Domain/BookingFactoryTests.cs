using FluentAssertions;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Events.Bookings;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class BookingFactoryTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ParkingId = Guid.NewGuid();
    private static readonly DateTime Start = DateTime.UtcNow.AddHours(2);
    private static readonly DateTime End = DateTime.UtcNow.AddHours(4);

    [Fact]
    public void CreateMarketplace_StartsPending_WithReference()
    {
        var booking = Booking.CreateMarketplace(
            UserId, ParkingId, Start, End, PricingType.Hourly, VehicleType.Car,
            100, 10, 5, 0, 115);

        booking.Status.Should().Be(BookingStatus.Pending);
        booking.UserId.Should().Be(UserId);
        booking.ParkingSpaceId.Should().Be(ParkingId);
        booking.TotalAmount.Should().Be(115);
        booking.BookingReference.Should().NotBeNullOrWhiteSpace();
        booking.DomainEvents.Should().ContainSingle(e => e is BookingRequestedEvent);
    }

    [Fact]
    public void CreateMarketplace_InvalidWindow_Throws()
    {
        var act = () => Booking.CreateMarketplace(
            UserId, ParkingId, End, Start, PricingType.Hourly, VehicleType.Car,
            0, 0, 0, 0, 0);

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void CreateCorporateEmployee_IsConfirmed_AndRaisesEvent()
    {
        var booking = Booking.CreateCorporateEmployee(
            UserId, ParkingId, Start, End, VehicleType.Car, 0, "KA01AB1234");

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.QRCode.Should().NotBeNullOrWhiteSpace();
        booking.DomainEvents.Should().ContainSingle(e => e is BookingConfirmedEvent);
    }

    [Fact]
    public void CreateCorporateVisitor_IsConfirmed()
    {
        var booking = Booking.CreateCorporateVisitor(
            UserId, ParkingId, Start, End, 50, "VISITOR1");

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.VehicleNumber.Should().Be("VISITOR1");
        booking.DomainEvents.Should().ContainSingle(e => e is BookingConfirmedEvent);
    }

    [Fact]
    public void UpdateVehicleDetails_OnPending_Succeeds()
    {
        var booking = Booking.CreateMarketplace(
            UserId, ParkingId, Start, End, PricingType.Hourly, VehicleType.Car,
            0, 0, 0, 0, 0);

        booking.UpdateVehicleDetails(VehicleType.SUV, "ka01xx9999", "Model X");

        booking.VehicleType.Should().Be(VehicleType.SUV);
        booking.VehicleNumber.Should().Be("KA01XX9999");
        booking.VehicleModel.Should().Be("Model X");
    }

    [Fact]
    public void AssignSlot_And_SetQrCode()
    {
        var booking = Booking.CreateCorporateEmployee(
            UserId, ParkingId, Start, End, VehicleType.Car, 0);

        booking.AssignSlot(7);
        booking.SetQrCode("TOKEN-ABC");

        booking.SlotNumber.Should().Be(7);
        booking.QRCode.Should().Be("TOKEN-ABC");
    }

    [Fact]
    public void ApplyPricing_UpdatesAmounts()
    {
        var booking = Booking.CreateMarketplace(
            UserId, ParkingId, Start, End, PricingType.Hourly, VehicleType.Car,
            0, 0, 0, 0, 0);
        var passId = Guid.NewGuid();

        booking.ApplyPricing(80, 8, 2, 10, 80, passId, null);

        booking.BaseAmount.Should().Be(80);
        booking.TotalAmount.Should().Be(80);
        booking.ParkingPassId.Should().Be(passId);
        booking.DiscountCode.Should().BeNull();
    }
}
