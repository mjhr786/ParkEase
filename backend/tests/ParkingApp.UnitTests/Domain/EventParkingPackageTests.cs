using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class EventParkingPackageTests
{
    [Fact]
    public void Create_And_Reserve_Until_SoldOut()
    {
        var package = EventParkingPackage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Concert Night",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(6),
            packagePrice: 400m,
            totalSpots: 2,
            eventName: "Big Show");

        package.IsOnSale(DateTime.UtcNow).Should().BeTrue();
        package.TryReserveSale(DateTime.UtcNow).Should().BeTrue();
        package.SoldCount.Should().Be(1);
        package.AvailableSpots.Should().Be(1);
        package.TryReserveSale(DateTime.UtcNow).Should().BeTrue();
        package.TryReserveSale(DateTime.UtcNow).Should().BeFalse();
        package.AvailableSpots.Should().Be(0);
        package.VenueEventId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_Reuses_VenueEventId_And_Buffers()
    {
        var venueEventId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddHours(4);

        var package = EventParkingPackage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "VIP Zone",
            start,
            end,
            900m,
            10,
            venueEventId: venueEventId,
            zoneName: "VIP Garage",
            earlyEntryMinutes: 60,
            lateExitMinutes: 30);

        package.VenueEventId.Should().Be(venueEventId);
        package.ZoneName.Should().Be("VIP Garage");
        package.AccessStartUtc.Should().Be(start.AddMinutes(-60));
        package.AccessEndUtc.Should().Be(end.AddMinutes(30));
    }

    [Fact]
    public void IsOnSale_False_BeforeSalesStart()
    {
        var package = EventParkingPackage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Future Sale",
            DateTime.UtcNow.AddDays(5),
            DateTime.UtcNow.AddDays(5).AddHours(4),
            100m,
            10,
            salesStartUtc: DateTime.UtcNow.AddDays(1));

        package.IsOnSale(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void CreateFromEventPackage_Uses_Access_Window()
    {
        var start = DateTime.UtcNow.AddHours(12);
        var end = DateTime.UtcNow.AddHours(18);
        var package = EventParkingPackage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Match Day",
            start,
            end,
            500m,
            50,
            earlyEntryMinutes: 45,
            lateExitMinutes: 20);
        package.TryReserveSale(DateTime.UtcNow);

        var booking = Booking.CreateFromEventPackage(
            Guid.NewGuid(),
            package,
            VehicleType.Car,
            taxAmount: 90m,
            serviceFee: 25m,
            totalAmount: 615m,
            vehicleNumber: "KA01AB1234");

        booking.Status.Should().Be(BookingStatus.AwaitingPayment);
        booking.EventParkingPackageId.Should().Be(package.Id);
        booking.StartDateTime.Should().Be(package.AccessStartUtc);
        booking.EndDateTime.Should().Be(package.AccessEndUtc);
        booking.BaseAmount.Should().Be(500m);
        booking.TotalAmount.Should().Be(615m);
        booking.BookingReference.Should().StartWith("EVT");
    }

    [Fact]
    public void CreateFromEventPackage_Confirmed_WhenFree()
    {
        var package = EventParkingPackage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Free Fan Zone",
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(8),
            0m,
            20);
        package.TryReserveSale(DateTime.UtcNow);

        var booking = Booking.CreateFromEventPackage(
            Guid.NewGuid(),
            package,
            VehicleType.Car,
            taxAmount: 0m,
            serviceFee: 0m,
            totalAmount: 0m);

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.QRCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ReleaseSale_DecrementsSoldCount()
    {
        var package = EventParkingPackage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pkg",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(3),
            100m,
            5);
        package.TryReserveSale(DateTime.UtcNow);
        package.ReleaseSale();
        package.SoldCount.Should().Be(0);
    }
}
