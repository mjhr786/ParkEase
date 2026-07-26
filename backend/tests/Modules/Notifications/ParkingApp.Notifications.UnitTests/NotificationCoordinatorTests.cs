using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Identity.Contracts;
using ParkingApp.Messaging.Contracts;
using ParkingApp.Notifications.Application.Services;
using ParkingApp.Notifications.Contracts;
using InboxNotificationType = ParkingApp.Messaging.Contracts.Enums.NotificationType;
using InboxNotificationPriority = ParkingApp.Messaging.Contracts.Enums.NotificationPriority;

namespace ParkingApp.Notifications.UnitTests;

public class NotificationCoordinatorTests
{
    private readonly Mock<INotificationService> _inApp = new();
    private readonly Mock<ISmsNotificationService> _sms = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly Mock<INotificationInbox> _inbox = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<ILogger<NotificationCoordinator>> _logger = new();
    private readonly Guid _userId = Guid.NewGuid();

    public NotificationCoordinatorTests()
    {
        _inbox.Setup(x => x.AddAsync(
                It.IsAny<Guid>(),
                It.IsAny<InboxNotificationType>(),
                It.IsAny<InboxNotificationPriority>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _inApp.Setup(x => x.NotifyUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _push.Setup(x => x.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushResult(true, "PUSH-1", SuccessCount: 1));
        _sms.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsResult(true, "SMS-1", Status: SmsStatus.Sent));
    }

    private NotificationCoordinator Create() =>
        new(_inApp.Object, _sms.Object, _push.Object, _inbox.Object, _users.Object, _logger.Object);

    [Fact]
    public async Task Send_InAppAndPush_WritesInboxAndChannels()
    {
        var request = new NotificationRequest(
            Type: "booking.confirmed",
            Title: "Confirmed",
            Message: "Your booking is ready",
            Channels: NotificationChannels.InApp | NotificationChannels.Push,
            Data: new Dictionary<string, string> { ["bookingId"] = Guid.NewGuid().ToString() },
            Priority: NotificationPriority.Normal);

        await Create().SendAsync(_userId, request);

        _inbox.Verify(x => x.AddAsync(
            _userId,
            It.IsAny<InboxNotificationType>(),
            InboxNotificationPriority.Normal,
            "Confirmed",
            "Your booking is ready",
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _inApp.Verify(x => x.NotifyUserAsync(_userId, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _push.Verify(x => x.SendToUserAsync(_userId, It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()), Times.Once);
        _sms.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Send_Sms_SkipsWhenPriorityLow()
    {
        var request = new NotificationRequest(
            "info", "T", "M",
            NotificationChannels.Sms,
            Priority: NotificationPriority.Normal);

        await Create().SendAsync(_userId, request);

        _sms.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Send_Sms_SendsWhenHighPriorityAndPhonePresent()
    {
        _users.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "u@x.com", "Ada", "Lovelace", PhoneNumber: "+919999999999"));

        var request = new NotificationRequest(
            "alert", "Urgent", "Please act",
            NotificationChannels.Sms,
            Priority: NotificationPriority.High);

        await Create().SendAsync(_userId, request);

        _sms.Verify(x => x.SendAsync("+919999999999", It.Is<string>(m => m.Contains("Urgent")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendBulk_SendsToEachUser()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var request = new NotificationRequest("info", "T", "M", NotificationChannels.InApp);

        await Create().SendBulkAsync(ids, request);

        _inbox.Verify(x => x.AddAsync(
            It.IsAny<Guid>(),
            It.IsAny<InboxNotificationType>(),
            It.IsAny<InboxNotificationPriority>(),
            "T",
            "M",
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
