using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Notifications.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Notifications;

public class NotificationCoordinatorTests
{
    private readonly Mock<INotificationService> _inAppMock;
    private readonly Mock<ISmsNotificationService> _smsMock;
    private readonly Mock<IPushNotificationService> _pushMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ILogger<NotificationCoordinator>> _loggerMock;
    private readonly NotificationCoordinator _service;

    public NotificationCoordinatorTests()
    {
        _inAppMock = new Mock<INotificationService>();
        _smsMock = new Mock<ISmsNotificationService>();
        _pushMock = new Mock<IPushNotificationService>();
        _uowMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<NotificationCoordinator>>();

        // Setup common mocks
        _uowMock.Setup(u => u.Notifications.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Notification());
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _inAppMock.Setup(i => i.NotifyUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _pushMock.Setup(p => p.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<PushNotificationPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushResult(true, "mockId", null, 1, 0));

        var userMock = new User { Id = Guid.NewGuid(), PhoneNumber = "+1234567890" };
        _uowMock.Setup(u => u.Users.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userMock);

        _smsMock.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsResult(true, "smsId", null, SmsStatus.Sent));

        _service = new NotificationCoordinator(_inAppMock.Object, _smsMock.Object, _pushMock.Object, _uowMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task SendAsync_IncludesAllConfiguredChannels()
    {
        // Arrange
        var request = new NotificationRequest(
            "BookingConfirmed",
            "Booking Confirmed",
            "Your booking was confirmed.",
            NotificationChannels.InApp | NotificationChannels.Push | NotificationChannels.Sms,
            null,
            NotificationPriority.High);
        
        var userId = Guid.NewGuid();

        // Act
        await _service.SendAsync(userId, request, CancellationToken.None);

        // Assert
        _uowMock.Verify(u => u.Notifications.AddAsync(It.Is<Notification>(n => n.UserId == userId && n.Title == request.Title), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _inAppMock.Verify(i => i.NotifyUserAsync(userId, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _pushMock.Verify(p => p.SendToUserAsync(userId, It.Is<PushNotificationPayload>(pay => pay.Title == request.Title), It.IsAny<CancellationToken>()), Times.Once);
        _smsMock.Verify(s => s.SendAsync("+1234567890", $"{request.Title}: {request.Message}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_LowPriority_SkipsSmsEvenIfConfigured()
    {
        // Arrange
        var request = new NotificationRequest(
            "Info",
            "Update Available",
            "Check out new features.",
            NotificationChannels.Sms | NotificationChannels.InApp,
            null,
            NotificationPriority.Low);
        
        var userId = Guid.NewGuid();

        // Act
        await _service.SendAsync(userId, request, CancellationToken.None);

        // Assert
        _inAppMock.Verify(i => i.NotifyUserAsync(userId, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _smsMock.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_UserWithNoPhoneNumber_SkipsSms()
    {
        // Arrange
        _uowMock.Setup(u => u.Users.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = Guid.NewGuid(), PhoneNumber = null });
        
        var request = new NotificationRequest(
            "Alert",
            "Emergency",
            "Important alert.",
            NotificationChannels.Sms,
            null,
            NotificationPriority.High);
        
        var userId = Guid.NewGuid();

        // Act
        await _service.SendAsync(userId, request, CancellationToken.None);

        // Assert
        _smsMock.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_HandlesExceptionsInChannels()
    {
        // Arrange
        _inAppMock.Setup(i => i.NotifyUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SignalR Down"));
            
        var request = new NotificationRequest(
            "Info",
            "Test",
            "Test MSG",
            NotificationChannels.InApp,
            null,
            NotificationPriority.Low);
            
        var userId = Guid.NewGuid();

        // Act
        var exception = await Record.ExceptionAsync(() => _service.SendAsync(userId, request, CancellationToken.None));

        // Assert
        exception.Should().BeNull(); // Should catch exception internally
    }

    [Fact]
    public async Task SendBulkAsync_SendsToAllUsers()
    {
        // Arrange
        var userIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var request = new NotificationRequest("Test", "Test", "Test", NotificationChannels.InApp, null, NotificationPriority.Normal);

        // Act
        await _service.SendBulkAsync(userIds, request, CancellationToken.None);

        // Assert
        _uowMock.Verify(u => u.Notifications.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _inAppMock.Verify(i => i.NotifyUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
