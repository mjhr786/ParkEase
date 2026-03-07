using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Notifications.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Notifications;

public class MockPushNotificationServiceTests
{
    private readonly Mock<ILogger<MockPushNotificationService>> _loggerMock;
    private readonly MockPushNotificationService _service;

    public MockPushNotificationServiceTests()
    {
        _loggerMock = new Mock<ILogger<MockPushNotificationService>>();
        _service = new MockPushNotificationService(_loggerMock.Object);
    }

    [Fact]
    public async Task SendToDeviceAsync_ReturnsSuccess()
    {
        // Arrange
        var payload = new PushNotificationPayload("Title", "Body", null, null, PushPriority.Normal);

        // Act
        var result = await _service.SendToDeviceAsync("device_token", payload, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.MessageId.Should().StartWith("PUSH-");
        result.SuccessCount.Should().Be(1);
    }

    [Fact]
    public async Task SendToUserAsync_UnregisteredUser_ReturnsMockSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var payload = new PushNotificationPayload("Title", "Body", null, null, PushPriority.Normal);

        // Act
        var result = await _service.SendToUserAsync(userId, payload, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.MessageId.Should().StartWith("PUSH-");
    }

    [Fact]
    public async Task SendToUserAsync_RegisteredUser_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _service.RegisterUserDevice(userId, "token_1");
        _service.RegisterUserDevice(userId, "token_2");
        var payload = new PushNotificationPayload("Title", "Body", null, null, PushPriority.Normal);

        // Act
        var result = await _service.SendToUserAsync(userId, payload, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SuccessCount.Should().Be(2); // Sent to 2 devices
    }

    [Fact]
    public async Task TopicSubscription_WorksProperly()
    {
        // Act
        var subResult = await _service.SubscribeToTopicAsync("token", "news");
        var subResultAgain = await _service.SubscribeToTopicAsync("token", "news");
        var unsubResult = await _service.UnsubscribeFromTopicAsync("token", "news");
        var unsubResultAgain = await _service.UnsubscribeFromTopicAsync("token", "news");

        // Assert
        subResult.Should().BeTrue();
        subResultAgain.Should().BeFalse(); // Already subscribed
        unsubResult.Should().BeTrue();
        unsubResultAgain.Should().BeFalse(); // Already unsubscribed
    }

    [Fact]
    public async Task SendToTopicAsync_ReturnsSuccess()
    {
        // Arrange
        var payload = new PushNotificationPayload("Title", "Body", null, null, PushPriority.Normal);
        await _service.SubscribeToTopicAsync("token1", "news");
        await _service.SubscribeToTopicAsync("token2", "news");

        // Act
        var result = await _service.SendToTopicAsync("news", payload, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SuccessCount.Should().Be(2);
    }
}
