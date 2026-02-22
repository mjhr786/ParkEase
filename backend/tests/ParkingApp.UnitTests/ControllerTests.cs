using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.CQRS.Commands.Auth;
using ParkingApp.Application.CQRS.Commands.Users;
using ParkingApp.Domain.Enums;
using FluentValidation;
using System.Security.Claims;
using FluentValidation.Results;
using ParkingApp.Application.CQRS.Commands.Chat;
using ParkingApp.Application.CQRS.Queries.Chat;

namespace ParkingApp.UnitTests;

public class ControllerTests
{
    private readonly Mock<IDispatcher> _mockDispatcher;
    private readonly Mock<IValidator<LoginDto>> _mockLoginValidator;
    private readonly Mock<IValidator<RegisterDto>> _mockRegisterValidator;

    public ControllerTests()
    {
        _mockDispatcher = new Mock<IDispatcher>();
        _mockLoginValidator = new Mock<IValidator<LoginDto>>();
        _mockRegisterValidator = new Mock<IValidator<RegisterDto>>();
    }

    [Fact]
    public async Task AuthController_Login_WithInvalidDto_ShouldReturnBadRequest()
    {
        // Arrange
        var controller = new AuthController(_mockDispatcher.Object, _mockRegisterValidator.Object, _mockLoginValidator.Object);
        var dto = new LoginDto("invalid", "");
        
        _mockLoginValidator.Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Email", "Invalid") }));

        // Act
        var result = await controller.Login(dto, default);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AuthController_Login_WhenSuccessful_ShouldReturnOk()
    {
        // Arrange
        var controller = new AuthController(_mockDispatcher.Object, _mockRegisterValidator.Object, _mockLoginValidator.Object);
        var dto = new LoginDto("test@test.com", "Password123!");
        var tokenDto = new TokenDto("access", "refresh", DateTime.UtcNow.AddHours(1), new UserDto(Guid.NewGuid(), "test@test.com", "John", "Doe", "123", UserRole.Member, true, true, DateTime.UtcNow));
        var apiResponse = new ApiResponse<TokenDto>(true, "Success", tokenDto);

        _mockLoginValidator.Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await controller.Login(dto, default);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(apiResponse);
    }

    [Fact]
    public async Task UsersController_Me_WhenAuthenticated_ShouldReturnUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var controller = new UsersController(_mockDispatcher.Object);
        
        // Mock User Claims
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var userDto = new UserDto(userId, "test@test.com", "John", "Doe", "123", UserRole.Member, true, true, DateTime.UtcNow);
        var apiResponse = new ApiResponse<UserDto>(true, null, userDto);

        _mockDispatcher.Setup(d => d.QueryAsync(It.IsAny<GetCurrentUserQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await controller.GetCurrentUser(default);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(apiResponse);
    }

    [Fact]
    public async Task ChatController_GetConversations_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var controller = new ChatController(_mockDispatcher.Object, new Mock<Microsoft.AspNetCore.SignalR.IHubContext<ParkingApp.Notifications.Hubs.ChatHub>>().Object);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) } };

        var listDto = new ConversationListDto(new List<ConversationDto>(), 0, 1, 20, 0);
        var apiResponse = new ApiResponse<ConversationListDto>(true, null, listDto);

        _mockDispatcher.Setup(d => d.QueryAsync(It.Is<GetConversationsQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await controller.GetConversations(1, 20, default);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(apiResponse);
    }

    [Fact]
    public async Task ChatController_GetMessages_ShouldReturnOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var controller = new ChatController(_mockDispatcher.Object, new Mock<Microsoft.AspNetCore.SignalR.IHubContext<ParkingApp.Notifications.Hubs.ChatHub>>().Object);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) } };

        var messages = new List<ChatMessageDto>();
        var apiResponse = new ApiResponse<List<ChatMessageDto>>(true, null, messages);

        _mockDispatcher.Setup(d => d.QueryAsync(It.Is<GetMessagesQuery>(q => q.ConversationId == convId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await controller.GetMessages(convId, 1, 50, default);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(apiResponse);
    }

    [Fact]
    public async Task ChatController_SendMessage_ShouldReturnOkAndBroadcast()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var dto = new SendMessageDto(Guid.NewGuid(), "Hello");
        var chatMessageDto = new ChatMessageDto(Guid.NewGuid(), convId, userId, "Sender", "Hello", false, DateTime.UtcNow);
        var apiResponse = new ApiResponse<ChatMessageDto>(true, null, chatMessageDto);

        var mockHubContext = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<ParkingApp.Notifications.Hubs.ChatHub>>();
        var mockClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
        var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

        var controller = new ChatController(_mockDispatcher.Object, mockHubContext.Object);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) } };

        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<SendMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);
        _mockDispatcher.Setup(d => d.QueryAsync(It.IsAny<GetConversationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ConversationListDto>(true, null, new ConversationListDto(new List<ConversationDto> { new ConversationDto(convId, Guid.NewGuid(), "Title", otherUserId, "Vendor Name", "preview", null, 0, DateTime.UtcNow) }, 1, 1, 20, 1)));

        // Act
        var result = await controller.SendMessage(dto, default);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(apiResponse);
        mockClientProxy.Verify(c => c.SendCoreAsync("ReceiveMessage", new object[] { chatMessageDto }, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ChatController_MarkAsRead_ShouldReturnOkAndBroadcast()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var convId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var apiResponse = new ApiResponse<bool>(true, null, true);

        var mockHubContext = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<ParkingApp.Notifications.Hubs.ChatHub>>();
        var mockClients = new Mock<Microsoft.AspNetCore.SignalR.IHubClients>();
        var mockClientProxy = new Mock<Microsoft.AspNetCore.SignalR.IClientProxy>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

        var controller = new ChatController(_mockDispatcher.Object, mockHubContext.Object);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) } };

        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<MarkMessagesReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);
        _mockDispatcher.Setup(d => d.QueryAsync(It.IsAny<GetConversationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ConversationListDto>(true, null, new ConversationListDto(new List<ConversationDto> { new ConversationDto(convId, Guid.NewGuid(), "Title", otherUserId, "Vendor Name", "preview", null, 0, DateTime.UtcNow) }, 1, 1, 20, 1)));

        // Act
        var result = await controller.MarkAsRead(convId, default);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(apiResponse);
        mockClientProxy.Verify(c => c.SendCoreAsync("MessagesRead", new object[] { convId }, It.IsAny<CancellationToken>()), Times.Once);
    }
}
