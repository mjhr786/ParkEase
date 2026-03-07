using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Notifications.Hubs;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace ParkingApp.UnitTests.Notifications;

public class NotificationHubTests
{
    private readonly Mock<ILogger<NotificationHub>> _loggerMock;
    private readonly Mock<IGroupManager> _groupManagerMock;
    private readonly Mock<HubCallerContext> _hubCallerContextMock;
    private readonly NotificationHub _hub;

    public NotificationHubTests()
    {
        _loggerMock = new Mock<ILogger<NotificationHub>>();
        _groupManagerMock = new Mock<IGroupManager>();

        _hubCallerContextMock = new Mock<HubCallerContext>();
        _hubCallerContextMock.Setup(c => c.ConnectionId).Returns("conn_123");

        _hub = new NotificationHub(_loggerMock.Object)
        {
            Context = _hubCallerContextMock.Object,
            Groups = _groupManagerMock.Object
        };
    }

    private void SetupUserClaim(string claimType, string claimValue)
    {
        var claims = new[] { new Claim(claimType, claimValue) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        _hubCallerContextMock.Setup(c => c.User).Returns(claimsPrincipal);
    }

    [Fact]
    public async Task OnConnectedAsync_WithValidUserId_AddsUserToGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUserClaim(ClaimTypes.NameIdentifier, userId.ToString());

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _groupManagerMock.Verify(g => g.AddToGroupAsync("conn_123", $"user_{userId}", default), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_WithValidSubClaim_AddsUserToGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUserClaim("sub", userId.ToString());

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _groupManagerMock.Verify(g => g.AddToGroupAsync("conn_123", $"user_{userId}", default), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_WithNoUserId_DoesNotThrow()
    {
        // Arrange
        // No claims setup

        // Act
        var exception = await Record.ExceptionAsync(() => _hub.OnConnectedAsync());

        // Assert
        exception.Should().BeNull();
        _groupManagerMock.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithUserId_LogsDisconnection()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUserClaim(ClaimTypes.NameIdentifier, userId.ToString());

        // Act
        var exception = await Record.ExceptionAsync(() => _hub.OnDisconnectedAsync(new Exception("Test disconnect error")));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public async Task AcknowledgeNotification_WithUserId_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupUserClaim(ClaimTypes.NameIdentifier, userId.ToString());

        // Act
        var exception = await Record.ExceptionAsync(() => _hub.AcknowledgeNotification("notif_123"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void GetUserGroupName_ReturnsCorrectFormat()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var groupName = NotificationHub.GetUserGroupName(userId);

        // Assert
        groupName.Should().Be($"user_{userId}");
    }
}
