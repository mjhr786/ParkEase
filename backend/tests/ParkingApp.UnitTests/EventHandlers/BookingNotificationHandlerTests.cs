using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Application.Interfaces;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Notifications.Application.EventHandlers;

namespace ParkingApp.UnitTests.EventHandlers;

public class BookingNotificationHandlerTests
{
    private readonly Mock<IParkingSpaceLookup> _parking = new();
    private readonly Mock<INotificationSender> _sender = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<IEmailService> _email = new();

    public BookingNotificationHandlerTests()
    {
        _sender
            .Setup(x => x.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationSendRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _email.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
    }

    private static ParkingSpaceSummary Summary(Guid spaceId, Guid ownerId, string title = "Lot A") =>
        new(spaceId, ownerId, title, true, 10, "IndividualVendor");

    [Fact]
    public async Task BookingRequested_WhenParkingMissing_DoesNotNotify()
    {
        _parking.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpaceSummary?)null);

        var handler = new BookingRequestedNotificationHandler(
            _parking.Object, _sender.Object, NullLogger<BookingRequestedNotificationHandler>.Instance);

        await handler.HandleAsync(new BookingRequestedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "BK1"));

        _sender.Verify(x => x.SendAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationSendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookingRequested_WhenOwnerPresent_NotifiesOwner()
    {
        var ownerId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Summary(spaceId, ownerId));

        var handler = new BookingRequestedNotificationHandler(
            _parking.Object, _sender.Object, NullLogger<BookingRequestedNotificationHandler>.Instance);

        var bookingId = Guid.NewGuid();
        await handler.HandleAsync(new BookingRequestedEvent(
            bookingId, Guid.NewGuid(), spaceId, "BK-REF"));

        _sender.Verify(x => x.SendAsync(
            ownerId,
            It.Is<NotificationSendRequest>(r =>
                r.Title.Contains("Booking Request") &&
                r.Data!["BookingId"] == bookingId.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookingApproved_WhenRequiresPayment_MentionsPayment()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Summary(spaceId, Guid.NewGuid(), "Lot B"));

        var handler = new BookingApprovedNotificationHandler(_parking.Object, _sender.Object);
        await handler.HandleAsync(new BookingApprovedEvent(
            Guid.NewGuid(), userId, spaceId, "BK2", RequiresPayment: true));

        _sender.Verify(x => x.SendAsync(
            userId,
            It.Is<NotificationSendRequest>(r => r.Message.Contains("payment", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookingConfirmed_NotifiesGuest()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Summary(spaceId, Guid.NewGuid(), "Lot C"));

        var handler = new BookingConfirmedNotificationHandler(_parking.Object, _sender.Object);
        await handler.HandleAsync(new BookingConfirmedEvent(
            Guid.NewGuid(), userId, spaceId, "BK3"));

        _sender.Verify(x => x.SendAsync(
            userId,
            It.IsAny<NotificationSendRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookingRejected_NotifiesGuest()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Summary(spaceId, Guid.NewGuid(), "Lot D"));

        var handler = new BookingRejectedNotificationHandler(
            _parking.Object,
            _users.Object,
            _sender.Object,
            _email.Object,
            NullLogger<BookingRejectedNotificationHandler>.Instance);

        await handler.HandleAsync(new BookingRejectedEvent(
            Guid.NewGuid(), userId, spaceId, "BK4", "Full", Guid.NewGuid()));

        _sender.Verify(x => x.SendAsync(
            userId,
            It.IsAny<NotificationSendRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
