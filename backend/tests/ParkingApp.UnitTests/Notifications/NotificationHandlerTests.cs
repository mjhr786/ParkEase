using ParkingApp.Notifications.Application.Queries.Notifications;
using ParkingApp.Notifications.Application.Commands.Notifications;
using Moq;
using FluentAssertions;
using Xunit;
using ParkingApp.Messaging.Contracts;
using ParkingApp.Messaging.Contracts.Enums;

namespace ParkingApp.UnitTests.Notifications;

public class NotificationHandlerTests
{
    private readonly Mock<INotificationInbox> _inbox = new();

    [Fact]
    public async Task MarkNotificationAsReadCommandHandler_WhenFound_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        _inbox.Setup(i => i.MarkAsReadAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new MarkNotificationAsReadCommandHandler(_inbox.Object);
        var result = await handler.HandleAsync(new MarkNotificationAsReadCommand(notificationId, userId));

        result.Success.Should().BeTrue();
        _inbox.Verify(i => i.MarkAsReadAsync(notificationId, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkNotificationAsReadCommandHandler_WhenNotFound_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        _inbox.Setup(i => i.MarkAsReadAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new MarkNotificationAsReadCommandHandler(_inbox.Object);
        var result = await handler.HandleAsync(new MarkNotificationAsReadCommand(notificationId, userId));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Notification not found");
    }

    [Fact]
    public async Task MarkAllNotificationsAsReadCommandHandler_ShouldCallInbox()
    {
        var userId = Guid.NewGuid();
        var handler = new MarkAllNotificationsAsReadCommandHandler(_inbox.Object);

        var result = await handler.HandleAsync(new MarkAllNotificationsAsReadCommand(userId));

        result.Success.Should().BeTrue();
        _inbox.Verify(i => i.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteNotificationCommandHandler_WhenFound_ShouldSucceed()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        _inbox.Setup(i => i.DeleteAsync(notificationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new DeleteNotificationCommandHandler(_inbox.Object);
        var result = await handler.HandleAsync(new DeleteNotificationCommand(notificationId, userId));

        result.Success.Should().BeTrue();
        _inbox.Verify(i => i.DeleteAsync(notificationId, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyNotificationsQueryHandler_ShouldReturnPagedResults()
    {
        var userId = Guid.NewGuid();
        var records = new List<NotificationRecord>
        {
            new(Guid.NewGuid(), userId, NotificationType.SystemAlert, "T1", "M1", null, false, DateTime.UtcNow),
            new(Guid.NewGuid(), userId, NotificationType.SystemAlert, "T2", "M2", null, true, DateTime.UtcNow)
        };

        _inbox.Setup(i => i.GetPagedAsync(userId, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(records);
        _inbox.Setup(i => i.GetTotalCountAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _inbox.Setup(i => i.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new GetMyNotificationsQueryHandler(_inbox.Object);
        var result = await handler.HandleAsync(new GetMyNotificationsQuery(userId, 1, 20));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.UnreadCount.Should().Be(1);
        result.Data.Notifications.Items.Should().HaveCount(2);
        result.Data.Notifications.TotalCount.Should().Be(2);
        result.Data.Notifications.TotalPages.Should().Be(1);
    }
}
