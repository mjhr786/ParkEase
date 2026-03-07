using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<ILogger<AuthService>> _mockLogger;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);
        _mockTokenService = new Mock<ITokenService>();
        _mockLogger = new Mock<ILogger<AuthService>>();

        _service = new AuthService(_mockUow.Object, _mockTokenService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task RegisterAsync_ShouldFail_WhenEmailExists()
    {
        var dto = new RegisterDto("test@test.com", "Password123", "First", "Last", "1234567890", UserRole.Member);
        _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email, It.IsAny<CancellationToken>())).ReturnsAsync(new User());

        var result = await _service.RegisterAsync(dto);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Email already registered");
    }

    [Fact]
    public async Task RegisterAsync_ShouldSucceed_WhenValid()
    {
        var dto = new RegisterDto("test@test.com", "Password123", "First", "Last", "1234567890", UserRole.Member);
        _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access_token");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh_token");

        var result = await _service.RegisterAsync(dto);

        result.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access_token");
        result.Data.RefreshToken.Should().Be("refresh_token");
        _mockUserRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldFail_WhenUserNotFound()
    {
        var dto = new LoginDto("test@test.com", "Password123");
        _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _service.LoginAsync(dto);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task LoginAsync_ShouldFail_WhenPasswordIncorrect()
    {
        var dto = new LoginDto("test@test.com", "Password123");
        var user = new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("DifferentPassword123!") };
        _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _service.LoginAsync(dto);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task LoginAsync_ShouldFail_WhenUserInactive()
    {
        var dto = new LoginDto("test@test.com", "Password123!");
        var user = new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), IsActive = false };
        _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _service.LoginAsync(dto);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Account disabled");
    }

    [Fact]
    public async Task LoginAsync_ShouldSucceed_WhenCredentialsValid()
    {
        var dto = new LoginDto("test@test.com", "Password123!");
        var user = new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"), IsActive = true };
        _mockUserRepo.Setup(r => r.GetByEmailAsync(dto.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access_token");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh_token");

        var result = await _service.LoginAsync(dto);

        result.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access_token");
        _mockUserRepo.Verify(r => r.Update(user), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldFail_WhenUserNotFound()
    {
        var dto = new RefreshTokenDto("refresh_token");
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(dto.RefreshToken, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _service.RefreshTokenAsync(dto);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid refresh token");
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldFail_WhenTokenInvalid()
    {
        var dto = new RefreshTokenDto("refresh_token");
        var user = new User();
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(dto.RefreshToken, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, dto.RefreshToken)).Returns(false);

        var result = await _service.RefreshTokenAsync(dto);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid refresh token");
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldSucceed_WhenValid()
    {
        var dto = new RefreshTokenDto("refresh_token");
        var user = new User();
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(dto.RefreshToken, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, dto.RefreshToken)).Returns(true);
        _mockTokenService.Setup(t => t.GenerateAccessToken(user)).Returns("new_access");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("new_refresh");

        var result = await _service.RefreshTokenAsync(dto);

        result.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("new_access");
        result.Data.RefreshToken.Should().Be("new_refresh");
        _mockUserRepo.Verify(r => r.Update(user), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_ShouldFail_WhenUserNotFound()
    {
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _service.LogoutAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_ShouldSucceed_WhenValid()
    {
        var user = new User { RefreshToken = "token", RefreshTokenExpiryTime = DateTime.UtcNow };
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _service.LogoutAsync(Guid.NewGuid());

        result.Success.Should().BeTrue();
        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();
        _mockUserRepo.Verify(r => r.Update(user), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldFail_WhenUserNotFound()
    {
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _service.ChangePasswordAsync(Guid.NewGuid(), new ChangePasswordDto("old", "new"));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldFail_WhenCurrentPasswordIncorrect()
    {
        var user = new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct123!") };
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _service.ChangePasswordAsync(Guid.NewGuid(), new ChangePasswordDto("Wrong123!", "New123!"));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid password");
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldSucceed_WhenValid()
    {
        var user = new User { PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct123!") };
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _service.ChangePasswordAsync(Guid.NewGuid(), new ChangePasswordDto("Correct123!", "New123!"));

        result.Success.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("New123!", user.PasswordHash).Should().BeTrue();
        user.RefreshToken.Should().BeNull();
        _mockUserRepo.Verify(r => r.Update(user), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
