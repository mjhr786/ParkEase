using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Application.Commands.Bookings;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.UnitTests.Bookings;

public class ValetHandlerTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _uow = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<INotificationSender> _notifications = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public ValetHandlerTests()
    {
        _uow.Setup(x => x.Bookings).Returns(_bookings.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _notifications
            .Setup(x => x.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationSendRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private (Booking booking, ParkingSpace space) CreateInProgressValetBooking(bool valetEnabled = true)
    {
        var space = ParkingSpace.CreateForVendor(
            _ownerId, "Valet Lot", "Desc", "1 Main", "City", "ST", "IN", "560001",
            12.9, 77.6, ParkingType.Open, 10, 50, 200, 1000, 3000);
        space.SetBayAndValet(bayGuidanceEnabled: true, valetEnabled: valetEnabled);

        var booking = Booking.CreateMarketplace(
            _userId, space.Id,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(3),
            PricingType.Hourly, VehicleType.Car,
            0, 0, 0, 0, 0);
        booking.Confirm();
        booking.CheckIn();
        booking.ParkingSpace = space;
        return (booking, space);
    }

    private static IOptionsMonitor<ValetOptions> Options(ValetOptions? opts = null)
    {
        var monitor = new Mock<IOptionsMonitor<ValetOptions>>();
        monitor.Setup(x => x.CurrentValue).Returns(opts ?? new ValetOptions
        {
            DefaultLeadMinutes = 15,
            MinLeadMinutes = 5,
            MaxLeadMinutes = 60
        });
        return monitor.Object;
    }

    [Fact]
    public async Task RequestValet_WhenBookingMissing_ReturnsNotFound()
    {
        _bookings.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        var handler = new RequestValetHandler(_uow.Object, _notifications.Object, Options(), NullLogger<RequestValetHandler>.Instance);
        var result = await handler.HandleAsync(new RequestValetCommand(Guid.NewGuid(), _userId, null, null));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Booking not found");
    }

    [Fact]
    public async Task RequestValet_WhenNotGuest_ReturnsUnauthorized()
    {
        var (booking, _) = CreateInProgressValetBooking();
        _bookings.Setup(x => x.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var handler = new RequestValetHandler(_uow.Object, _notifications.Object, Options(), NullLogger<RequestValetHandler>.Instance);
        var result = await handler.HandleAsync(new RequestValetCommand(booking.Id, Guid.NewGuid(), null, 10));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task RequestValet_WhenValetDisabled_ReturnsFailure()
    {
        var (booking, _) = CreateInProgressValetBooking(valetEnabled: false);
        _bookings.Setup(x => x.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var handler = new RequestValetHandler(_uow.Object, _notifications.Object, Options(), NullLogger<RequestValetHandler>.Instance);
        var result = await handler.HandleAsync(new RequestValetCommand(booking.Id, _userId, null, 10));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not available");
    }

    [Fact]
    public async Task RequestValet_WhenValid_NotifiesOwner()
    {
        var (booking, space) = CreateInProgressValetBooking();
        _bookings.Setup(x => x.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var handler = new RequestValetHandler(_uow.Object, _notifications.Object, Options(), NullLogger<RequestValetHandler>.Instance);
        var result = await handler.HandleAsync(new RequestValetCommand(booking.Id, _userId, "Gate B", 20));

        result.Success.Should().BeTrue();
        _notifications.Verify(x => x.SendAsync(
            space.OwnerId,
            It.IsAny<NotificationSendRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelValet_WhenGuest_SucceedsAfterRequest()
    {
        var (booking, _) = CreateInProgressValetBooking();
        booking.RequestValet(DateTime.UtcNow, 15, null);
        _bookings.Setup(x => x.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var handler = new CancelValetHandler(_uow.Object);
        var result = await handler.HandleAsync(new CancelValetCommand(booking.Id, _userId));

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AcknowledgeValet_WhenVendor_Succeeds()
    {
        var (booking, _) = CreateInProgressValetBooking();
        booking.RequestValet(DateTime.UtcNow, 15, null);
        _bookings.Setup(x => x.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var handler = new AcknowledgeValetHandler(_uow.Object);
        var result = await handler.HandleAsync(new AcknowledgeValetCommand(booking.Id, _ownerId));

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AssignBay_WhenVendor_Succeeds()
    {
        var (booking, _) = CreateInProgressValetBooking();
        _bookings.Setup(x => x.GetByIdWithDetailsAsync(booking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        var handler = new AssignBayHandler(_uow.Object);
        var result = await handler.HandleAsync(new AssignBayCommand(
            booking.Id, _ownerId, "B2", "Zone A", "A-12", 12));

        result.Success.Should().BeTrue();
    }
}
