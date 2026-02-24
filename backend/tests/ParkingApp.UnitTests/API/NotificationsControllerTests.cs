using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Notifications;
using ParkingApp.Application.CQRS.Queries.Notifications;
using ParkingApp.Application.DTOs;

namespace ParkingApp.UnitTests.API;

public class NotificationsControllerTests
{
    private readonly Mock<IDispatcher> _mockDispatcher;

    public NotificationsControllerTests()
    {
        _mockDispatcher = new Mock<IDispatcher>();
    }

    private NotificationsController CreateController(Guid userId)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "mock"));

        var controller = new NotificationsController(_mockDispatcher.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetMyNotifications_ShouldReturnOk_WithData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var controller = CreateController(userId);
        
        var mockResponse = new ApiResponse<NotificationListDto>(true, "Success", new NotificationListDto(null!, 0));
        _mockDispatcher.Setup(d => d.QueryAsync(It.Is<GetMyNotificationsQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await controller.GetMyNotifications();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(mockResponse);
    }

    [Fact]
    public async Task MarkAsRead_WhenSuccessful_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var controller = CreateController(userId);
        var notificationId = Guid.NewGuid();
        
        _mockDispatcher.Setup(d => d.SendAsync(It.Is<MarkNotificationAsReadCommand>(c => c.NotificationId == notificationId && c.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, "Success", true));

        // Act
        var result = await controller.MarkAsRead(notificationId);

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task MarkAllAsRead_WhenSuccessful_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var controller = CreateController(userId);
        
        _mockDispatcher.Setup(d => d.SendAsync(It.Is<MarkAllNotificationsAsReadCommand>(c => c.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, "Success", true));

        // Act
        var result = await controller.MarkAllAsRead();

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task Delete_WhenSuccessful_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var controller = CreateController(userId);
        var notificationId = Guid.NewGuid();
        
        _mockDispatcher.Setup(d => d.SendAsync(It.Is<DeleteNotificationCommand>(c => c.NotificationId == notificationId && c.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, "Success", true));

        // Act
        var result = await controller.Delete(notificationId);

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task Delete_WhenUnsuccessful_ShouldReturnBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var controller = CreateController(userId);
        var notificationId = Guid.NewGuid();
        
        _mockDispatcher.Setup(d => d.SendAsync(It.Is<DeleteNotificationCommand>(c => c.NotificationId == notificationId && c.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(false, "Not found", false));

        // Act
        var result = await controller.Delete(notificationId);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Not found");
    }
}
