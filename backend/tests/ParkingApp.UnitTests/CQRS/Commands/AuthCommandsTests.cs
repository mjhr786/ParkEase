using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.CQRS.Commands.Auth;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class AuthCommandsTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<ILogger<RegisterHandler>> _mockRegisterLogger;
    private readonly Mock<ILogger<LoginHandler>> _mockLoginLogger;
    private readonly Mock<ILogger<LogoutHandler>> _mockLogoutLogger;
    private readonly Mock<ILogger<ChangePasswordHandler>> _mockChangePasswordLogger;

    public AuthCommandsTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);

        _mockTokenService = new Mock<ITokenService>();
        _mockRegisterLogger = new Mock<ILogger<RegisterHandler>>();
        _mockLoginLogger = new Mock<ILogger<LoginHandler>>();
        _mockLogoutLogger = new Mock<ILogger<LogoutHandler>>();
        _mockChangePasswordLogger = new Mock<ILogger<ChangePasswordHandler>>();
    }

    // RegisterHandler Tests
    [Fact]
    public async Task RegisterHandler_ShouldFail_WhenEmailExists()
    {
        var handler = new RegisterHandler(_mockUow.Object, _mockTokenService.Object, _mockRegisterLogger.Object);
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new User());

        var res = await handler.HandleAsync(new RegisterCommand(new RegisterDto("test@test.com", "Pass123", "F", "L", "123", UserRole.Member)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Email already registered");
    }

    [Fact]
    public async Task RegisterHandler_ShouldSucceed()
    {
        var handler = new RegisterHandler(_mockUow.Object, _mockTokenService.Object, _mockRegisterLogger.Object);
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User)null);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("token");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh");

        var res = await handler.HandleAsync(new RegisterCommand(new RegisterDto("test@test.com", "Pass123", "F", "L", "123", UserRole.Member)));

        res.Success.Should().BeTrue();
        res.Data.AccessToken.Should().Be("token");
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // LoginHandler Tests
    [Fact]
    public async Task LoginHandler_ShouldFail_WhenUserNotFound()
    {
        var handler = new LoginHandler(_mockUow.Object, _mockTokenService.Object, _mockLoginLogger.Object);
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User)null);

        var res = await handler.HandleAsync(new LoginCommand(new LoginDto("test@test.com", "Pass123")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Invalid credentials");
    }

    [Fact]
    public async Task LoginHandler_ShouldFail_WhenPasswordIncorrect()
    {
        var handler = new LoginHandler(_mockUow.Object, _mockTokenService.Object, _mockLoginLogger.Object);
        var user = new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("RealPass") };
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new LoginCommand(new LoginDto("test@test.com", "WrongPass")));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task LoginHandler_ShouldSucceed()
    {
        var handler = new LoginHandler(_mockUow.Object, _mockTokenService.Object, _mockLoginLogger.Object);
        var user = new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass123"), IsActive = true };
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("token");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh");

        var res = await handler.HandleAsync(new LoginCommand(new LoginDto("test@test.com", "Pass123")));

        res.Success.Should().BeTrue();
        res.Data.AccessToken.Should().Be("token");
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // RefreshTokenHandler Tests
    [Fact]
    public async Task RefreshTokenHandler_ShouldFail_WhenInvalidToken()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User)null);

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Invalid refresh token");
    }

    [Fact]
    public async Task RefreshTokenHandler_ShouldSucceed()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        var user = new User();
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, It.IsAny<string>())).Returns(true);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("token2");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh2");

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token1")));

        res.Success.Should().BeTrue();
        res.Data.AccessToken.Should().Be("token2");
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // LogoutHandler Tests
    [Fact]
    public async Task LogoutHandler_ShouldSucceed()
    {
        var handler = new LogoutHandler(_mockUow.Object, _mockLogoutLogger.Object);
        var user = new User { RefreshToken = "token" };
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new LogoutCommand(Guid.NewGuid()));

        res.Success.Should().BeTrue();
        user.RefreshToken.Should().BeNull();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ChangePasswordHandler Tests
    [Fact]
    public async Task ChangePasswordHandler_ShouldFail_WhenPasswordIncorrect()
    {
        var handler = new ChangePasswordHandler(_mockUow.Object, _mockChangePasswordLogger.Object);
        var user = new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("RealPass") };
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new ChangePasswordCommand(Guid.NewGuid(), new ChangePasswordDto("WrongPass", "NewPass")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Invalid password");
    }

    [Fact]
    public async Task ChangePasswordHandler_ShouldSucceed()
    {
        var handler = new ChangePasswordHandler(_mockUow.Object, _mockChangePasswordLogger.Object);
        var user = new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pass123") };
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new ChangePasswordCommand(Guid.NewGuid(), new ChangePasswordDto("Pass123", "NewPass123")));

        res.Success.Should().BeTrue();
        var isMatch = BCrypt.Net.BCrypt.Verify("NewPass123", user.PasswordHash);
        isMatch.Should().BeTrue();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
