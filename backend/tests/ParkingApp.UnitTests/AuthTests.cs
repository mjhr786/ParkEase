using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Identity.Application.Commands.Auth;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Corporate.Domain;
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Identity.Domain.Interfaces;

namespace ParkingApp.UnitTests;

public class AuthTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;

    private readonly Mock<ILogger<LoginHandler>> _mockLoginLogger;
    private readonly Mock<ILogger<RegisterHandler>> _mockRegisterLogger;
    private readonly Mock<ILogger<LogoutHandler>> _mockLogoutLogger;
    private readonly Mock<ILogger<ChangePasswordHandler>> _mockPasswordLogger;

    public AuthTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockTokenService = new Mock<ITokenService>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();

        _mockLoginLogger = new Mock<ILogger<LoginHandler>>();
        _mockRegisterLogger = new Mock<ILogger<RegisterHandler>>();
        _mockLogoutLogger = new Mock<ILogger<LogoutHandler>>();
        _mockPasswordLogger = new Mock<ILogger<ChangePasswordHandler>>();

        _mockUnitOfWork.Setup(u => u.Users).Returns(_mockUserRepository.Object);

        _mockPasswordHasher
            .Setup(h => h.Hash(It.IsAny<string>()))
            .Returns((string password) => $"hash:{password}");
        _mockPasswordHasher
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string password, string hash) => hash == $"hash:{password}");
    }

    #region Login Tests

    [Fact]
    public async Task LoginHandler_WithValidCredentials_ShouldReturnToken()
    {
        var handler = new LoginHandler(
            _mockUnitOfWork.Object,
            _mockTokenService.Object,
            _mockPasswordHasher.Object,
            _mockLoginLogger.Object);
        var password = "Password123!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = $"hash:{password}",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "1",
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
        var handler = new RegisterHandler(
            _mockUnitOfWork.Object,
            _mockTokenService.Object,
            _mockPasswordHasher.Object,
            _mockRegisterLogger.Object);
        _mockUserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User());

        var dto = new RegisterDto("exist@test.com", "Pass123!", "First", "Last", "123");
        var result = await handler.HandleAsync(new RegisterCommand(dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Email already registered");
    }

    [Fact]
    public async Task RegisterHandler_WithValidData_ShouldCreateUserAndReturnToken()
    {
        var handler = new RegisterHandler(
            _mockUnitOfWork.Object,
            _mockTokenService.Object,
            _mockPasswordHasher.Object,
            _mockRegisterLogger.Object);
        _mockUserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");

        var dto = new RegisterDto("new@test.com", "Pass123!", "First", "Last", "123");
        var result = await handler.HandleAsync(new RegisterCommand(dto));

        result.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access-token");
        _mockUserRepository.Verify(r => r.AddAsync(
            It.Is<User>(u => u.PasswordHash == "hash:Pass123!"),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task LogoutHandler_WhenUserExists_ShouldClearRefreshToken()
    {
        var handler = new LogoutHandler(_mockUnitOfWork.Object, _mockLogoutLogger.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };
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
        var handler = new ChangePasswordHandler(
            _mockUnitOfWork.Object,
            _mockPasswordHasher.Object,
            _mockPasswordLogger.Object);
        var oldPassword = "OldPass123!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash:OldPass123!",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "1",
            IsActive = true
        };
        _mockUserRepository.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var dto = new ChangePasswordDto(oldPassword, "NewPass123!");
        var result = await handler.HandleAsync(new ChangePasswordCommand(user.Id, dto));

        result.Success.Should().BeTrue();
        user.PasswordHash.Should().Be("hash:NewPass123!");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}





