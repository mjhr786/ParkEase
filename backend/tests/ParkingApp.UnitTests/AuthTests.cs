using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS.Commands.Auth;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests;

public class AuthTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ITokenService> _mockTokenService;
    
    // Loggers (Generic versions for different handlers)
    private readonly Mock<ILogger<LoginHandler>> _mockLoginLogger;
    private readonly Mock<ILogger<RegisterHandler>> _mockRegisterLogger;
    private readonly Mock<ILogger<LogoutHandler>> _mockLogoutLogger;
    private readonly Mock<ILogger<ChangePasswordHandler>> _mockPasswordLogger;

    public AuthTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockTokenService = new Mock<ITokenService>();
        
        _mockLoginLogger = new Mock<ILogger<LoginHandler>>();
        _mockRegisterLogger = new Mock<ILogger<RegisterHandler>>();
        _mockLogoutLogger = new Mock<ILogger<LogoutHandler>>();
        _mockPasswordLogger = new Mock<ILogger<ChangePasswordHandler>>();

        _mockUnitOfWork.Setup(u => u.Users).Returns(_mockUserRepository.Object);
    }

    #region Login Tests
    
    [Fact]
    public async Task LoginHandler_WithValidCredentials_ShouldReturnToken()
    {
        var handler = new LoginHandler(_mockUnitOfWork.Object, _mockTokenService.Object, _mockLoginLogger.Object);
        var password = "Password123!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true
        };

        _mockUserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");

        var result = await handler.HandleAsync(new LoginCommand(new LoginDto("test@example.com", password)));

        result.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access-token");
        _mockUserRepository.Verify(r => r.Update(user), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Register Tests

    [Fact]
    public async Task RegisterHandler_WhenEmailExists_ShouldReturnFailure()
    {
        var handler = new RegisterHandler(_mockUnitOfWork.Object, _mockTokenService.Object, _mockRegisterLogger.Object);
        _mockUserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User());

        var dto = new RegisterDto("exist@test.com", "Pass123!", "First", "Last", "123", UserRole.Member);
        var result = await handler.HandleAsync(new RegisterCommand(dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Email already registered");
    }

    [Fact]
    public async Task RegisterHandler_WithValidData_ShouldCreateUserAndReturnToken()
    {
        var handler = new RegisterHandler(_mockUnitOfWork.Object, _mockTokenService.Object, _mockRegisterLogger.Object);
        _mockUserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");

        var dto = new RegisterDto("new@test.com", "Pass123!", "First", "Last", "123", UserRole.Member);
        var result = await handler.HandleAsync(new RegisterCommand(dto));

        result.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access-token");
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task LogoutHandler_WhenUserExists_ShouldClearRefreshToken()
    {
        var handler = new LogoutHandler(_mockUnitOfWork.Object, _mockLogoutLogger.Object);
        var user = new User { Id = Guid.NewGuid(), RefreshToken = "old-token" };
        _mockUserRepository.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await handler.HandleAsync(new LogoutCommand(user.Id));

        result.Success.Should().BeTrue();
        user.RefreshToken.Should().BeNull();
        _mockUserRepository.Verify(r => r.Update(user), Times.Once);
    }

    #endregion

    #region Change Password Tests

    [Fact]
    public async Task ChangePasswordHandler_WithCorrectOldPassword_ShouldUpdateHash()
    {
        var handler = new ChangePasswordHandler(_mockUnitOfWork.Object, _mockPasswordLogger.Object);
        var oldPassword = "OldPass123!";
        var user = new User { Id = Guid.NewGuid(), PasswordHash = BCrypt.Net.BCrypt.HashPassword(oldPassword) };
        _mockUserRepository.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var dto = new ChangePasswordDto(oldPassword, "NewPass123!");
        var result = await handler.HandleAsync(new ChangePasswordCommand(user.Id, dto));

        result.Success.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("NewPass123!", user.PasswordHash).Should().BeTrue();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
