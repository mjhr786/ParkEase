using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Notifications;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class NotificationCommandsTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;

    public NotificationCommandsTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockNotificationRepo = new Mock<INotificationRepository>();

        _mockUow.Setup(u => u.Notifications).Returns(_mockNotificationRepo.Object);
    }

    // MarkNotificationAsReadCommandHandler Tests
    [Fact]
    public async Task MarkNotificationAsReadCommandHandler_ShouldFail_WhenNotFoundOrUnauthorized()
    {
        var handler = new MarkNotificationAsReadCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var notification = new Notification { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _mockNotificationRepo.Setup(r => r.GetByIdAsync(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);

        var res = await handler.HandleAsync(new MarkNotificationAsReadCommand(notification.Id, userId));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task MarkNotificationAsReadCommandHandler_ShouldSucceed()
    {
        var handler = new MarkNotificationAsReadCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var notification = new Notification { Id = Guid.NewGuid(), UserId = userId, IsRead = false };
        _mockNotificationRepo.Setup(r => r.GetByIdAsync(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);

        var res = await handler.HandleAsync(new MarkNotificationAsReadCommand(notification.Id, userId));

        res.Success.Should().BeTrue();
        notification.IsRead.Should().BeTrue();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // MarkAllNotificationsAsReadCommandHandler Tests
    [Fact]
    public async Task MarkAllNotificationsAsReadCommandHandler_ShouldSucceed()
    {
        var handler = new MarkAllNotificationsAsReadCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();

        var res = await handler.HandleAsync(new MarkAllNotificationsAsReadCommand(userId));

        res.Success.Should().BeTrue();
        _mockNotificationRepo.Verify(r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // DeleteNotificationCommandHandler Tests
    [Fact]
    public async Task DeleteNotificationCommandHandler_ShouldFail_WhenNotFoundOrUnauthorized()
    {
        var handler = new DeleteNotificationCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var notification = new Notification { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _mockNotificationRepo.Setup(r => r.GetByIdAsync(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);

        var res = await handler.HandleAsync(new DeleteNotificationCommand(notification.Id, userId));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteNotificationCommandHandler_ShouldSucceed()
    {
        var handler = new DeleteNotificationCommandHandler(_mockUow.Object);
        var userId = Guid.NewGuid();
        var notification = new Notification { Id = Guid.NewGuid(), UserId = userId };
        _mockNotificationRepo.Setup(r => r.GetByIdAsync(notification.Id, It.IsAny<CancellationToken>())).ReturnsAsync(notification);

        var res = await handler.HandleAsync(new DeleteNotificationCommand(notification.Id, userId));

        res.Success.Should().BeTrue();
        _mockNotificationRepo.Verify(r => r.Remove(notification), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
