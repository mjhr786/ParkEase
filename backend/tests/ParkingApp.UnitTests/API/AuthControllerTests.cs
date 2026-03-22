using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Auth;
using ParkingApp.Application.CQRS.Commands.Users;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Common;
using Xunit;

namespace ParkingApp.UnitTests.API;

public class AuthControllerTests
{
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly Mock<IValidator<RegisterDto>> _registerValidatorMock;
    private readonly Mock<IValidator<LoginDto>> _loginValidatorMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _dispatcherMock = new Mock<IDispatcher>();
        _registerValidatorMock = new Mock<IValidator<RegisterDto>>();
        _loginValidatorMock = new Mock<IValidator<LoginDto>>();

        _controller = new AuthController(
            _dispatcherMock.Object, 
            _registerValidatorMock.Object, 
            _loginValidatorMock.Object);
    }

    [Fact]
    public async Task Register_WithValidDto_ReturnsCreated()
    {
        // Arrange
        var dto = new RegisterDto("test@test.com", "password", "Test", "User", "12345", ParkingApp.Domain.Enums.UserRole.Member);
        var tokenDto = new TokenDto("token", "refresh", DateTime.UtcNow.AddHours(1), new UserDto(Guid.NewGuid(), "test@test.com", "Test", "User", "12345", ParkingApp.Domain.Enums.UserRole.Member, true, true, DateTime.UtcNow));
        var result = new ApiResponse<TokenDto>(true, "Success", tokenDto, null);

        _registerValidatorMock
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
            
        _dispatcherMock
            .Setup(d => d.SendAsync(It.Is<RegisterCommand>(c => c.Dto == dto), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(result));

        // Act
        var actionResult = await _controller.Register(dto, CancellationToken.None);

        // Assert
        var createdResult = actionResult.Should().BeOfType<CreatedResult>().Subject;
        var apiResponse = createdResult.Value.Should().BeOfType<ApiResponse<TokenDto>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(tokenDto);
    }

    [Fact]
    public async Task Register_WithInvalidDto_ReturnsBadRequest()
    {
        // Arrange
        var dto = new RegisterDto("invalid", "password", "Test", "User", "12345", ParkingApp.Domain.Enums.UserRole.Member);
        var validationResult = new ValidationResult(new[] { new ValidationFailure("Email", "Invalid email") });

        _registerValidatorMock
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var actionResult = await _controller.Register(dto, CancellationToken.None);

        // Assert
        var badRequestResult = actionResult.Should().BeOfType<BadRequestObjectResult>().Subject;
    }

    [Fact]
    public async Task Login_WithValidDto_ReturnsOk()
    {
         // Arrange
        var dto = new LoginDto("test@test.com", "password");
        var tokenDto = new TokenDto("token", "refresh", DateTime.UtcNow.AddHours(1), new UserDto(Guid.NewGuid(), "test@test.com", "Test", "User", "12345", ParkingApp.Domain.Enums.UserRole.Member, true, true, DateTime.UtcNow));
        var result = new ApiResponse<TokenDto>(true, "Success", tokenDto, null);

        _loginValidatorMock
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
            
        _dispatcherMock
            .Setup(d => d.SendAsync(It.Is<LoginCommand>(c => c.Dto == dto), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(result));

        // Act
        var actionResult = await _controller.Login(dto, CancellationToken.None);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
         // Arrange
        var dto = new LoginDto("test@test.com", "wrong");
        var result = new ApiResponse<TokenDto>(false, "Invalid credentials", null, new List<string> { "Invalid credentials" });

        _loginValidatorMock
            .Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
            
        _dispatcherMock
            .Setup(d => d.SendAsync(It.Is<LoginCommand>(c => c.Dto == dto), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(result));

        // Act
        var actionResult = await _controller.Login(dto, CancellationToken.None);

        // Assert
        var unauthorizedResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
    }

    [Fact]
    public async Task RefreshToken_WithValidDto_ReturnsOk()
    {
        // Arrange
        var dto = new RefreshTokenDto("refresh");
        var tokenDto = new TokenDto("new-token", "new-refresh", DateTime.UtcNow.AddHours(1), new UserDto(Guid.NewGuid(), "test@test.com", "Test", "User", "12345", ParkingApp.Domain.Enums.UserRole.Member, true, true, DateTime.UtcNow));
        var result = new ApiResponse<TokenDto>(true, "Success", tokenDto, null);

        _dispatcherMock
            .Setup(d => d.SendAsync(It.Is<RefreshTokenCommand>(c => c.Dto == dto), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(result));

        // Act
        var actionResult = await _controller.RefreshToken(dto, CancellationToken.None);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
    }

    [Fact]
    public async Task Logout_WhenAuthenticated_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        
        _dispatcherMock
            .Setup(d => d.SendAsync(It.Is<LogoutCommand>(c => c.UserId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<bool>(true, "Success", true, null)));

        // Act
        var actionResult = await _controller.Logout(CancellationToken.None);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
    }

    [Fact]
    public async Task ChangePassword_WhenAuthenticated_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        var dto = new ChangePasswordDto("old", "new");
        
        _dispatcherMock
            .Setup(d => d.SendAsync(It.Is<ChangePasswordCommand>(c => c.UserId == userId && c.Dto == dto), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<bool>(true, "Success", true, null)));

        // Act
        var actionResult = await _controller.ChangePassword(dto, CancellationToken.None);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
    }

    private void SetupControllerUser(ControllerBase controller, Guid userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }
}

public class UsersControllerTests
{
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _dispatcherMock = new Mock<IDispatcher>();
        _controller = new UsersController(_dispatcherMock.Object);
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticated_ReturnsOk()
    {
         // Arrange
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        var userDto = new UserDto(userId, "test@test.com", "Test", "User", "12345", ParkingApp.Domain.Enums.UserRole.Member, true, true, DateTime.UtcNow);
        
        _dispatcherMock
            .Setup(d => d.QueryAsync(It.Is<GetCurrentUserQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<UserDto>(true, "Success", userDto, null)));

        // Act
        var actionResult = await _controller.GetCurrentUser(CancellationToken.None);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
    }

    [Fact]
    public async Task UpdateCurrentUser_WhenAuthenticated_ReturnsOk()
    {
         // Arrange
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        var dto = new UpdateUserDto("NewName", null, null);
        var userDto = new UserDto(userId, "test@test.com", "NewName", "User", "12345", ParkingApp.Domain.Enums.UserRole.Member, true, true, DateTime.UtcNow);
        
        _dispatcherMock
            .Setup(d => d.SendAsync(It.Is<UpdateUserCommand>(c => c.UserId == userId && c.Dto == dto), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<UserDto>(true, "Success", userDto, null)));

        // Act
        var actionResult = await _controller.UpdateCurrentUser(dto, CancellationToken.None);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
    }

    [Fact]
    public async Task DeleteCurrentUser_WhenAuthenticated_ReturnsOk()
    {
         // Arrange
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        
        _dispatcherMock
            .Setup(d => d.SendAsync(It.Is<DeleteUserCommand>(c => c.UserId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<bool>(true, "Success", true, null)));

        // Act
        var actionResult = await _controller.DeleteCurrentUser(CancellationToken.None);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
    }

    private void SetupControllerUser(ControllerBase controller, Guid userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }
}
