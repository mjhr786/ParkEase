using FluentAssertions;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Application.Commands.Bookings;
using ParkingApp.Marketplace.Application.Queries.Bookings;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.UnitTests.Bookings;

/// <summary>
/// KD-19: consumer My Bookings detail/cancel must exclude corporate-staged marketplace rows.
/// Vendor owners may still view staged rows for their spaces.
/// </summary>
public class CorporateStagedBookingConsumerTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _uow = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Guid _guestId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public CorporateStagedBookingConsumerTests()
    {
        _uow.Setup(x => x.Bookings).Returns(_bookings.Object);
    }

    private (Booking booking, ParkingSpace space) CreateCorporateStagedBooking()
    {
        var space = ParkingSpace.CreateForVendor(
            _ownerId, "Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);

        var booking = Booking.CreateCorporateEmployee(
            _guestId,
            space.Id,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            VehicleType.Car,
            0m,
            "KA01AB1234");
        booking.ParkingSpace = space;
        booking.IsCorporateStaged.Should().BeTrue();
        return (booking, space);
    }

    [Fact]
    public async Task GetBookingById_WhenCorporateStaged_AndCallerIsGuest_ReturnsNotFound()
    {
        var (booking, _) = CreateCorporateStagedBooking();
        _bookings.Setup(x => x.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var handler = new GetBookingByIdHandler(_uow.Object);
        var result = await handler.HandleAsync(new GetBookingByIdQuery(booking.Id, _guestId));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Booking not found");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetBookingById_WhenCorporateStaged_AndCallerIsOwner_ReturnsOk()
    {
        var (booking, _) = CreateCorporateStagedBooking();
        _bookings.Setup(x => x.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var handler = new GetBookingByIdHandler(_uow.Object);
        var result = await handler.HandleAsync(new GetBookingByIdQuery(booking.Id, _ownerId));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(booking.Id);
    }

    [Fact]
    public async Task GetBookingById_WhenMarketplaceBooking_GuestCanViewOwn()
    {
        var space = ParkingSpace.CreateForVendor(
            _ownerId, "Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        var booking = Booking.CreateMarketplace(
            _guestId, space.Id,
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3),
            PricingType.Hourly, VehicleType.Car,
            100, 10, 5, 0, 115);
        booking.ParkingSpace = space;
        booking.IsCorporateStaged.Should().BeFalse();

        _bookings.Setup(x => x.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var handler = new GetBookingByIdHandler(_uow.Object);
        var result = await handler.HandleAsync(new GetBookingByIdQuery(booking.Id, _guestId));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelBooking_WhenCorporateStaged_ReturnsNotFound()
    {
        var (booking, _) = CreateCorporateStagedBooking();
        _bookings.Setup(x => x.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var email = new Mock<IEmailService>();
        var users = new Mock<IUserLookup>();
        var handler = new CancelBookingHandler(_uow.Object, email.Object, users.Object);

        var result = await handler.HandleAsync(new CancelBookingCommand(booking.Id, _guestId, "changed plans"));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Booking not found");
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
