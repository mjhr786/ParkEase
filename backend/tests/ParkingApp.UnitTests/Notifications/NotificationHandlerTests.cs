using Moq;
using FluentAssertions;
using Xunit;
using ParkingApp.Application.CQRS.Commands.Notifications;
using ParkingApp.Application.CQRS.Queries.Notifications;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests.Notifications;

public class NotificationHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<INotificationRepository> _mockNotificationRepository;

    public NotificationHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockNotificationRepository = new Mock<INotificationRepository>();

        _mockUnitOfWork.Setup(u => u.Notifications).Returns(_mockNotificationRepository.Object);
    }

    [Fact]
    public async Task MarkNotificationAsReadCommandHandler_WhenNotificationExistsAndBelongsToUser_ShouldMarkAsRead()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var notification = new Notification { Id = notificationId, UserId = userId, IsRead = false };

        _mockNotificationRepository.Setup(r => r.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var handler = new MarkNotificationAsReadCommandHandler(_mockUnitOfWork.Object);
        var command = new MarkNotificationAsReadCommand(notificationId, userId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
        _mockNotificationRepository.Verify(r => r.Update(notification), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkNotificationAsReadCommandHandler_WhenNotificationNotFound_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        _mockNotificationRepository.Setup(r => r.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        var handler = new MarkNotificationAsReadCommandHandler(_mockUnitOfWork.Object);
        var command = new MarkNotificationAsReadCommand(notificationId, userId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Notification not found");
        _mockNotificationRepository.Verify(r => r.Update(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task MarkAllNotificationsAsReadCommandHandler_ShouldCallRepositoryAndSaveChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var handler = new MarkAllNotificationsAsReadCommandHandler(_mockUnitOfWork.Object);
        var command = new MarkAllNotificationsAsReadCommand(userId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        _mockNotificationRepository.Verify(r => r.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteNotificationCommandHandler_WhenNotificationExists_ShouldDelete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var notification = new Notification { Id = notificationId, UserId = userId };

        _mockNotificationRepository.Setup(r => r.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var handler = new DeleteNotificationCommandHandler(_mockUnitOfWork.Object);
        var command = new DeleteNotificationCommand(notificationId, userId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        _mockNotificationRepository.Verify(r => r.Remove(notification), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyNotificationsQueryHandler_ShouldReturnPagedResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notifications = new List<Notification>
        {
            new Notification { Id = Guid.NewGuid(), UserId = userId, Type = NotificationType.SystemAlert, Title = "T1", Message = "M1", IsRead = false },
            new Notification { Id = Guid.NewGuid(), UserId = userId, Type = NotificationType.SystemAlert, Title = "T2", Message = "M2", IsRead = true }
        };

        _mockNotificationRepository.Setup(r => r.GetPagedAsync(userId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);
        _mockNotificationRepository.Setup(r => r.GetTotalCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _mockNotificationRepository.Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new GetMyNotificationsQueryHandler(_mockUnitOfWork.Object);
        var query = new GetMyNotificationsQuery(userId, 1, 20);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.UnreadCount.Should().Be(1);
        result.Data.Notifications.Items.Should().HaveCount(2);
        result.Data.Notifications.TotalCount.Should().Be(2);
        result.Data.Notifications.TotalPages.Should().Be(1);
    }
}
