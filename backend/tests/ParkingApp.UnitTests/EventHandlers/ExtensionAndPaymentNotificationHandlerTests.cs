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

public class ExtensionAndPaymentNotificationHandlerTests
{
    private readonly Mock<IParkingSpaceLookup> _parking = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<INotificationSender> _sender = new();
    private readonly Mock<IEmailService> _email = new();

    public ExtensionAndPaymentNotificationHandlerTests()
    {
        _sender
            .Setup(x => x.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationSendRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _email.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
    }

    private static ParkingSpaceSummary Space(Guid id, Guid ownerId) =>
        new(id, ownerId, "Lot X", true, 10, "IndividualVendor");

    [Fact]
    public async Task ExtensionRequested_WhenParkingMissing_DoesNotNotify()
    {
        _parking.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpaceSummary?)null);

        var handler = new BookingExtensionRequestedNotificationHandler(
            _parking.Object, _users.Object, _sender.Object, _email.Object,
            NullLogger<BookingExtensionRequestedNotificationHandler>.Instance);

        await handler.HandleAsync(new BookingExtensionRequestedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "BK", DateTime.UtcNow.AddHours(2), 50m));

        _sender.Verify(x => x.SendAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationSendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExtensionRequested_NotifiesOwner()
    {
        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Space(spaceId, ownerId));
        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(userId, "g@x.com", "Guest", "One"));
        _users.Setup(x => x.GetByIdAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(ownerId, "o@x.com", "Owner", "One"));

        var handler = new BookingExtensionRequestedNotificationHandler(
            _parking.Object, _users.Object, _sender.Object, _email.Object,
            NullLogger<BookingExtensionRequestedNotificationHandler>.Instance);

        await handler.HandleAsync(new BookingExtensionRequestedEvent(
            Guid.NewGuid(), userId, spaceId, "BK-EXT", DateTime.UtcNow.AddHours(3), 100m));

        _sender.Verify(x => x.SendAsync(
            ownerId,
            It.Is<NotificationSendRequest>(r => r.Title.Contains("Extension")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtensionApproved_WhenRequiresPayment_MentionsPayment()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Space(spaceId, Guid.NewGuid()));
        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(userId, "g@x.com", "Guest", "One"));

        var handler = new BookingExtensionApprovedNotificationHandler(
            _parking.Object, _users.Object, _sender.Object, _email.Object);

        await handler.HandleAsync(new BookingExtensionApprovedEvent(
            Guid.NewGuid(), userId, spaceId, "BK", true, 75m, DateTime.UtcNow.AddHours(4), Guid.NewGuid()));

        _sender.Verify(x => x.SendAsync(
            userId,
            It.Is<NotificationSendRequest>(r =>
                r.Title.Contains("Approved") &&
                r.Message.Contains("payment", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExtensionRejected_NotifiesGuest()
    {
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Space(spaceId, Guid.NewGuid()));
        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(userId, "g@x.com", "Guest", "One"));

        var handler = new BookingExtensionRejectedNotificationHandler(
            _parking.Object, _users.Object, _sender.Object, _email.Object);

        await handler.HandleAsync(new BookingExtensionRejectedEvent(
            Guid.NewGuid(), userId, spaceId, "BK", "No capacity", Guid.NewGuid()));

        _sender.Verify(x => x.SendAsync(
            userId,
            It.Is<NotificationSendRequest>(r => r.Title.Contains("Rejected")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaymentCompleted_WhenExtensionPayment_Skips()
    {
        var handler = new PaymentCompletedNotificationHandler(
            _parking.Object, _users.Object, _sender.Object, _email.Object,
            NullLogger<PaymentCompletedNotificationHandler>.Instance);

        await handler.HandleAsync(new PaymentCompletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "BK", 50m, "INR", IsExtensionPayment: true));

        _sender.Verify(x => x.SendAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationSendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PaymentCompleted_WhenBookingPayment_NotifiesOwner()
    {
        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _parking.Setup(x => x.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Space(spaceId, ownerId));
        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(userId, "g@x.com", "Guest", "One"));
        _users.Setup(x => x.GetByIdAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(ownerId, "o@x.com", "Owner", "One"));

        var handler = new PaymentCompletedNotificationHandler(
            _parking.Object, _users.Object, _sender.Object, _email.Object,
            NullLogger<PaymentCompletedNotificationHandler>.Instance);

        await handler.HandleAsync(new PaymentCompletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), userId, spaceId,
            "BK-PAY", 200m, "INR", IsExtensionPayment: false));

        _sender.Verify(x => x.SendAsync(
            ownerId,
            It.Is<NotificationSendRequest>(r => r.Title.Contains("Payment")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
