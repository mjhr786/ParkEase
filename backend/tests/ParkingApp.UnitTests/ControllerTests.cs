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
}
