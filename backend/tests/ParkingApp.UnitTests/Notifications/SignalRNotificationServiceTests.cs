using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Notifications.Hubs;
using ParkingApp.Notifications.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Notifications;

public class SignalRNotificationServiceTests
{
    private readonly Mock<IHubContext<NotificationHub>> _hubContextMock;
    private readonly Mock<IHubClients> _hubClientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<ILogger<SignalRNotificationService>> _loggerMock;
    private readonly SignalRNotificationService _service;

    public SignalRNotificationServiceTests()
    {
        _hubContextMock = new Mock<IHubContext<NotificationHub>>();
        _hubClientsMock = new Mock<IHubClients>();
        _clientProxyMock = new Mock<IClientProxy>();
        _loggerMock = new Mock<ILogger<SignalRNotificationService>>();

        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
        _hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _hubClientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);

        _service = new SignalRNotificationService(_hubContextMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task NotifyUserAsync_SendsToCorrectGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationDto = new NotificationDto("Alert", "Title", "Message", null);
        var expectedGroup = $"user_{userId}";

        // Act
        await _service.NotifyUserAsync(userId, notificationDto, CancellationToken.None);

        // Assert
        _hubClientsMock.Verify(c => c.Group(expectedGroup), Times.Once);
        _clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveNotification", new object[] { notificationDto }, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyUsersAsync_SendsToMultipleGroups()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var notificationDto = new NotificationDto("Alert", "Title", "Message", null);

        // Act
        await _service.NotifyUsersAsync(new[] { userId1, userId2 }, notificationDto, CancellationToken.None);

        // Assert
        _hubClientsMock.Verify(c => c.Group($"user_{userId1}"), Times.Once);
        _hubClientsMock.Verify(c => c.Group($"user_{userId2}"), Times.Once);
        _clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveNotification", new object[] { notificationDto }, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task BroadcastAsync_SendsToAllClients()
    {
        // Arrange
        var notificationDto = new NotificationDto("Alert", "Title", "Message", null);

        // Act
        await _service.BroadcastAsync(notificationDto, CancellationToken.None);

        // Assert
        _hubClientsMock.Verify(c => c.All, Times.Once);
        _clientProxyMock.Verify(c => c.SendCoreAsync("ReceiveNotification", new object[] { notificationDto }, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyUserAsync_WithException_DoesNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notificationDto = new NotificationDto("Alert", "Title", "Message", null);
        
        _hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Throws(new Exception("Hub error"));

        // Act
        var exception = await Record.ExceptionAsync(() => _service.NotifyUserAsync(userId, notificationDto, CancellationToken.None));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public async Task BroadcastAsync_WithException_DoesNotThrow()
    {
        // Arrange
        var notificationDto = new NotificationDto("Alert", "Title", "Message", null);
        
        _hubClientsMock.Setup(c => c.All).Throws(new Exception("Hub error"));

        // Act
        var exception = await Record.ExceptionAsync(() => _service.BroadcastAsync(notificationDto, CancellationToken.None));

        // Assert
        exception.Should().BeNull();
    }
}
