using ParkingApp.Notifications.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using INotificationService = ParkingApp.Notifications.Contracts.INotificationService;
using ISmsNotificationService = ParkingApp.Notifications.Contracts.ISmsNotificationService;
using IPushNotificationService = ParkingApp.Notifications.Contracts.IPushNotificationService;
using ParkingApp.Identity.Contracts;
using ParkingApp.Messaging.Contracts;
using InboxType = ParkingApp.Messaging.Contracts.Enums.NotificationType;
using InboxPriority = ParkingApp.Messaging.Contracts.Enums.NotificationPriority;
using ParkingApp.Notifications.Application.Services;
using Xunit;

namespace ParkingApp.UnitTests.Notifications;

public class NotificationCoordinatorTests
{
    private readonly Mock<INotificationService> _inAppMock = new();
    private readonly Mock<ISmsNotificationService> _smsMock = new();
    private readonly Mock<IPushNotificationService> _pushMock = new();
    private readonly Mock<INotificationInbox> _inboxMock = new();
    private readonly Mock<IUserLookup> _usersMock = new();
    private readonly Mock<ILogger<NotificationCoordinator>> _loggerMock = new();
    private readonly NotificationCoordinator _service;

    public NotificationCoordinatorTests()
    {
        _inboxMock.Setup(i => i.AddAsync(
                It.IsAny<Guid>(),
                It.IsAny<InboxType>(),
                It.IsAny<InboxPriority>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _inAppMock.Setup(i => i.NotifyUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pushMock.Setup(p => p.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushResult(true, "mockId", null, 1, 0));

        _usersMock.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(Guid.NewGuid(), "a@b.com", "A", "B", "+1234567890"));

        _smsMock.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsResult(true, "smsId", null, SmsStatus.Sent));

        _service = new NotificationCoordinator(
            _inAppMock.Object,
            _smsMock.Object,
            _pushMock.Object,
            _inboxMock.Object,
            _usersMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task SendAsync_IncludesAllConfiguredChannels()
    {
        var request = new NotificationRequest(
            "BookingConfirmed",
            "Booking Confirmed",
            "Your booking was confirmed.",
            NotificationChannels.InApp | NotificationChannels.Push | NotificationChannels.Sms,
            null,
            NotificationPriority.High);

        var userId = Guid.NewGuid();

        await _service.SendAsync(userId, request, CancellationToken.None);

        _inboxMock.Verify(i => i.AddAsync(
            userId,
            It.IsAny<InboxType>(),
            It.IsAny<InboxPriority>(),
            request.Title,
            request.Message,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _inAppMock.Verify(i => i.NotifyUserAsync(userId, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _pushMock.Verify(p => p.SendToUserAsync(userId, It.Is<PushNotificationPayload>(pay => pay.Title == request.Title), It.IsAny<CancellationToken>()), Times.Once);
        _smsMock.Verify(s => s.SendAsync("+1234567890", $"{request.Title}: {request.Message}", It.IsAny<CancellationToken>()), Times.Once);
    }
}
