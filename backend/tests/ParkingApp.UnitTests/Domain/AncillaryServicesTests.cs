using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class AncillaryServicesTests
{
    [Fact]
    public void ParkingAncillaryService_Create_NormalizesAndValidates()
    {
        var spaceId = Guid.NewGuid();
        var service = ParkingAncillaryService.Create(
            spaceId,
            "  Basic wash  ",
            299.999m,
            " Exterior only ",
            durationMinutes: 30,
            sortOrder: 1);

        service.ParkingSpaceId.Should().Be(spaceId);
        service.Name.Should().Be("Basic wash");
        service.Description.Should().Be("Exterior only");
        service.Price.Should().Be(300.00m);
        service.DurationMinutes.Should().Be(30);
        service.IsActive.Should().BeTrue();
        service.SortOrder.Should().Be(1);
    }

    [Fact]
    public void ParkingAncillaryService_Create_RejectsNegativePrice()
    {
        var act = () => ParkingAncillaryService.Create(Guid.NewGuid(), "Wash", -1m);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ParkingAncillaryService_UpdateAndDeactivate()
    {
        var service = ParkingAncillaryService.Create(Guid.NewGuid(), "Basic", 100m);
        service.Update(name: "Premium", price: 199.5m, isActive: true, sortOrder: 2);
        service.Name.Should().Be("Premium");
        service.Price.Should().Be(199.50m);
        service.SortOrder.Should().Be(2);

        service.Deactivate();
        service.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Booking_AddAncillaryLine_SnapshotsAndSubtotals()
    {
        var booking = Booking.CreateMarketplace(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            PricingType.Hourly,
            VehicleType.Car,
            baseAmount: 250m,
            taxAmount: 45m,
            serviceFee: 12.5m,
            discountAmount: 0m,
            totalAmount: 307.5m);

        var serviceId = Guid.NewGuid();
        booking.AddAncillaryLine("Basic wash", 299m, quantity: 1, serviceId: serviceId);
        booking.AddAncillaryLine("Interior detail", 150m, quantity: 2, serviceId: Guid.NewGuid());

        booking.AncillaryLines.Should().HaveCount(2);
        booking.AncillarySubtotal.Should().Be(599m);
        booking.AncillaryLines.First().ServiceId.Should().Be(serviceId);
        booking.AncillaryLines.First().SnapshotName.Should().Be("Basic wash");
        booking.AncillaryLines.Last().LineTotal.Should().Be(300m);
    }

    [Fact]
    public void BookingAncillaryLine_RejectsInvalidQuantity()
    {
        var act = () => BookingAncillaryLine.Create(Guid.NewGuid(), "Wash", 100m, quantity: 0);
        act.Should().Throw<ValidationException>();
    }
}
