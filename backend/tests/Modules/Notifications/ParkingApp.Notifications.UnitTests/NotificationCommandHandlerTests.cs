using FluentAssertions;
using Moq;
using ParkingApp.Messaging.Contracts;
using ParkingApp.Notifications.Application.Commands.Notifications;

namespace ParkingApp.Notifications.UnitTests;

public class NotificationCommandHandlerTests
{
    private readonly Mock<INotificationInbox> _inbox = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _notificationId = Guid.NewGuid();

    [Fact]
    public async Task MarkAsRead_WhenFound_ReturnsSuccess()
    {
        _inbox.Setup(x => x.MarkAsReadAsync(_notificationId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await new MarkNotificationAsReadCommandHandler(_inbox.Object)
            .HandleAsync(new MarkNotificationAsReadCommand(_notificationId, _userId));

        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsRead_WhenMissing_ReturnsFailure()
    {
        _inbox.Setup(x => x.MarkAsReadAsync(_notificationId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await new MarkNotificationAsReadCommandHandler(_inbox.Object)
            .HandleAsync(new MarkNotificationAsReadCommand(_notificationId, _userId));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task MarkAllAsRead_Succeeds()
    {
        _inbox.Setup(x => x.MarkAllAsReadAsync(_userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await new MarkAllNotificationsAsReadCommandHandler(_inbox.Object)
            .HandleAsync(new MarkAllNotificationsAsReadCommand(_userId));

        result.Success.Should().BeTrue();
        _inbox.Verify(x => x.MarkAllAsReadAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenFound_Succeeds()
    {
        _inbox.Setup(x => x.DeleteAsync(_notificationId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await new DeleteNotificationCommandHandler(_inbox.Object)
            .HandleAsync(new DeleteNotificationCommand(_notificationId, _userId));

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WhenMissing_Fails()
    {
        _inbox.Setup(x => x.DeleteAsync(_notificationId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await new DeleteNotificationCommandHandler(_inbox.Object)
            .HandleAsync(new DeleteNotificationCommand(_notificationId, _userId));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAll_Succeeds()
    {
        _inbox.Setup(x => x.DeleteAllAsync(_userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await new ClearAllNotificationsCommandHandler(_inbox.Object)
            .HandleAsync(new ClearAllNotificationsCommand(_userId));

        result.Success.Should().BeTrue();
        _inbox.Verify(x => x.DeleteAllAsync(_userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
