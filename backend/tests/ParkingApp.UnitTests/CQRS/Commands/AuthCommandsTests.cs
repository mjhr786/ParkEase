using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Identity.Application.Commands.Auth;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.BuildingBlocks.Security;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Enums;
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Identity.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class AuthCommandsTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ITokenService> _mockTokenService;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
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
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockRegisterLogger = new Mock<ILogger<RegisterHandler>>();
        _mockLoginLogger = new Mock<ILogger<LoginHandler>>();
        _mockLogoutLogger = new Mock<ILogger<LogoutHandler>>();
        _mockChangePasswordLogger = new Mock<ILogger<ChangePasswordHandler>>();

        _mockPasswordHasher
            .Setup(h => h.Hash(It.IsAny<string>()))
            .Returns((string password) => $"hash:{password}");
        _mockPasswordHasher
            .Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string password, string hash) => hash == $"hash:{password}");
    }

    [Fact]
    public async Task RegisterHandler_ShouldFail_WhenEmailExists()
    {
        var handler = new RegisterHandler(
            _mockUow.Object,
            _mockTokenService.Object,
            _mockPasswordHasher.Object,
            _mockRegisterLogger.Object);
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new User());

        var res = await handler.HandleAsync(new RegisterCommand(new RegisterDto("test@test.com", "Pass123", "F", "L", "123")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Email already registered");
    }

    [Fact]
    public async Task RegisterHandler_ShouldSucceed()
    {
        var handler = new RegisterHandler(
            _mockUow.Object,
            _mockTokenService.Object,
            _mockPasswordHasher.Object,
            _mockRegisterLogger.Object);
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User)null!);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>())).Returns("token");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh");

        var res = await handler.HandleAsync(new RegisterCommand(new RegisterDto("test@test.com", "Pass123", "F", "L", "123")));

        res.Success.Should().BeTrue();
        res.Data!.AccessToken.Should().Be("token");
        res.Data.Channel.Should().Be(nameof(ProductChannel.Marketplace));
        _mockTokenService.Verify(t => t.GenerateAccessToken(It.IsAny<User>(), ProductChannel.Marketplace, null, null), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginHandler_ShouldFail_WhenUserNotFound()
    {
        var handler = new LoginHandler(
            _mockUow.Object,
            _mockTokenService.Object,
            _mockPasswordHasher.Object,
            _mockLoginLogger.Object);
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User)null!);

        var res = await handler.HandleAsync(new LoginCommand(new LoginDto("test@test.com", "Pass123")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Invalid credentials");
    }

    [Fact]
    public async Task LoginHandler_ShouldFail_WhenPasswordIncorrect()
    {
        var handler = new LoginHandler(
            _mockUow.Object,
            _mockTokenService.Object,
            _mockPasswordHasher.Object,
            _mockLoginLogger.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new LoginCommand(new LoginDto("test@test.com", "WrongPass")));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task LoginHandler_ShouldSucceed()
    {
        var handler = new LoginHandler(
            _mockUow.Object,
            _mockTokenService.Object,
            _mockPasswordHasher.Object,
            _mockLoginLogger.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash:Pass123", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true, Role = UserRole.User };
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>())).Returns("token");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh");

        var res = await handler.HandleAsync(new LoginCommand(new LoginDto("test@test.com", "Pass123")));

        res.Success.Should().BeTrue();
        res.Data!.AccessToken.Should().Be("token");
        res.Data.Channel.Should().Be(nameof(ProductChannel.Marketplace));
        user.SessionChannel.Should().Be(ProductChannel.Marketplace);
        _mockTokenService.Verify(t => t.GenerateAccessToken(user, ProductChannel.Marketplace, null, null), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginHandler_AdminUser_ShouldMintAdminChannel()
    {
        var handler = new LoginHandler(
            _mockUow.Object,
            _mockTokenService.Object,
            _mockPasswordHasher.Object,
            _mockLoginLogger.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            PasswordHash = "hash:Pass123",
            FirstName = "A",
            LastName = "D",
            PhoneNumber = "1",
            IsActive = true,
            Role = UserRole.Admin
        };
        _mockUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>())).Returns("token");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh");

        var res = await handler.HandleAsync(new LoginCommand(new LoginDto("admin@example.com", "Pass123")));

        res.Success.Should().BeTrue();
        res.Data!.Channel.Should().Be(nameof(ProductChannel.Admin));
        user.SessionChannel.Should().Be(ProductChannel.Admin);
        _mockTokenService.Verify(t => t.GenerateAccessToken(user, ProductChannel.Admin, null, null), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenHandler_ShouldFail_WhenInvalidToken()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User)null!);

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Invalid refresh token");
    }

    [Fact]
    public async Task RefreshTokenHandler_ShouldSucceed()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "T",
            LastName = "U",
            PhoneNumber = "1",
            IsActive = true,
            Role = UserRole.User
        };
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, It.IsAny<string>())).Returns(true);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>())).Returns("token2");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh2");

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token1")));

        res.Success.Should().BeTrue();
        res.Data!.AccessToken.Should().Be("token2");
        res.Data.Channel.Should().Be(nameof(ProductChannel.Marketplace));
        user.SessionChannel.Should().Be(ProductChannel.Marketplace);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenHandler_WhenSessionCorporate_BodyOmit_PreservesCorporate()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        var companyId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "corp@example.com",
            PasswordHash = "hash",
            FirstName = "C",
            LastName = "U",
            PhoneNumber = "1",
            IsActive = true
        };
        user.BindSession(ProductChannel.Corporate, companyId, "Admin");
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, It.IsAny<string>())).Returns(true);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>())).Returns("token2");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh2");

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token1")));

        res.Success.Should().BeTrue();
        res.Data!.Channel.Should().Be(nameof(ProductChannel.Corporate));
        res.Data.CompanyId.Should().Be(companyId);
        res.Data.CompanyRole.Should().Be("Admin");
        res.Data.IsBootstrap.Should().BeFalse();
        user.SessionChannel.Should().Be(ProductChannel.Corporate);
        user.SessionCompanyId.Should().Be(companyId);
        _mockTokenService.Verify(t => t.GenerateAccessToken(user, ProductChannel.Corporate, companyId, "Admin"), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenHandler_WhenSessionCorporate_ChannelNull_PreservesCorporate()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        var companyId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "corp@example.com",
            PasswordHash = "hash",
            FirstName = "C",
            LastName = "U",
            PhoneNumber = "1",
            IsActive = true
        };
        user.BindSession(ProductChannel.Corporate, companyId, "Employee");
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, It.IsAny<string>())).Returns(true);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>())).Returns("token2");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh2");

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token1", Channel: null)));

        res.Success.Should().BeTrue();
        res.Data!.Channel.Should().Be(nameof(ProductChannel.Corporate));
        res.Data.CompanyId.Should().Be(companyId);
        res.Data.CompanyRole.Should().Be("Employee");
        _mockTokenService.Verify(t => t.GenerateAccessToken(user, ProductChannel.Corporate, companyId, "Employee"), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenHandler_WhenSessionCorporate_RebindMarketplace_DemotesAndClearsCompany()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        var companyId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "corp@example.com",
            PasswordHash = "hash",
            FirstName = "C",
            LastName = "U",
            PhoneNumber = "1",
            IsActive = true,
            Role = UserRole.User
        };
        user.BindSession(ProductChannel.Corporate, companyId, "Admin");
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, It.IsAny<string>())).Returns(true);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>())).Returns("token2");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh2");

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token1", Channel: "Marketplace")));

        res.Success.Should().BeTrue();
        res.Data!.Channel.Should().Be(nameof(ProductChannel.Marketplace));
        res.Data.CompanyId.Should().BeNull();
        res.Data.CompanyRole.Should().BeNull();
        res.Data.IsBootstrap.Should().BeNull();
        user.SessionChannel.Should().Be(ProductChannel.Marketplace);
        user.SessionCompanyId.Should().BeNull();
        user.SessionCompanyRole.Should().BeNull();
        _mockTokenService.Verify(t => t.GenerateAccessToken(user, ProductChannel.Marketplace, null, null), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenHandler_WhenChannelCorporate_RejectsUnvalidatedRebind()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "u@example.com",
            PasswordHash = "hash",
            FirstName = "U",
            LastName = "S",
            PhoneNumber = "1",
            IsActive = true,
            Role = UserRole.User
        };
        user.BindSession(ProductChannel.Marketplace);
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, It.IsAny<string>())).Returns(true);

        var companyId = Guid.NewGuid();
        var res = await handler.HandleAsync(new RefreshTokenCommand(
            new RefreshTokenDto("token1", Channel: "Corporate", CompanyId: companyId)));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("channel_rebind_forbidden");
        user.SessionChannel.Should().Be(ProductChannel.Marketplace);
        user.SessionCompanyId.Should().BeNull();
        _mockTokenService.Verify(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenHandler_WhenChannelAdmin_AndUserNotAdmin_Rejects()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "u@example.com",
            PasswordHash = "hash",
            FirstName = "U",
            LastName = "S",
            PhoneNumber = "1",
            IsActive = true,
            Role = UserRole.User
        };
        user.BindSession(ProductChannel.Marketplace);
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, It.IsAny<string>())).Returns(true);

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token1", Channel: "Admin")));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("channel_rebind_forbidden");
        _mockTokenService.Verify(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenHandler_WhenChannelAdmin_AndUserIsAdmin_RebindsAdmin()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "D",
            PhoneNumber = "1",
            IsActive = true,
            Role = UserRole.Admin
        };
        user.BindSession(ProductChannel.Marketplace);
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, It.IsAny<string>())).Returns(true);
        _mockTokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>())).Returns("token2");
        _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh2");

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token1", Channel: "Admin")));

        res.Success.Should().BeTrue();
        res.Data!.Channel.Should().Be(nameof(ProductChannel.Admin));
        user.SessionChannel.Should().Be(ProductChannel.Admin);
        _mockTokenService.Verify(t => t.GenerateAccessToken(user, ProductChannel.Admin, null, null), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenHandler_WhenChannelInvalid_ReturnsInvalidChannelCode()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "u@example.com",
            PasswordHash = "hash",
            FirstName = "U",
            LastName = "S",
            PhoneNumber = "1",
            IsActive = true,
            Role = UserRole.User
        };
        user.BindSession(ProductChannel.Marketplace);
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, It.IsAny<string>())).Returns(true);

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token1", Channel: "Foo")));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("invalid_channel");
        res.Message.Should().Contain("Invalid channel");
        user.SessionChannel.Should().Be(ProductChannel.Marketplace);
        _mockTokenService.Verify(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenHandler_WhenChannelNumeric_Rejects()
    {
        var handler = new RefreshTokenHandler(_mockUow.Object, _mockTokenService.Object);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "u@example.com",
            PasswordHash = "hash",
            FirstName = "U",
            LastName = "S",
            PhoneNumber = "1",
            IsActive = true,
            Role = UserRole.User
        };
        user.BindSession(ProductChannel.Marketplace);
        _mockUserRepo.Setup(r => r.GetByRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockTokenService.Setup(t => t.ValidateRefreshToken(user, It.IsAny<string>())).Returns(true);

        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("token1", Channel: "2")));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("invalid_channel");
        _mockTokenService.Verify(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task LogoutHandler_ShouldSucceed_AndClearSession()
    {
        var handler = new LogoutHandler(_mockUow.Object, _mockLogoutLogger.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };
        user.BindSession(ProductChannel.Corporate, Guid.NewGuid(), "Admin");
        user.RotateRefreshToken("rt", DateTime.UtcNow.AddDays(1));
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new LogoutCommand(Guid.NewGuid()));

        res.Success.Should().BeTrue();
        user.RefreshToken.Should().BeNull();
        user.SessionChannel.Should().BeNull();
        user.SessionCompanyId.Should().BeNull();
        user.SessionCompanyRole.Should().BeNull();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordHandler_ShouldFail_WhenPasswordIncorrect()
    {
        var handler = new ChangePasswordHandler(
            _mockUow.Object,
            _mockPasswordHasher.Object,
            _mockChangePasswordLogger.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new ChangePasswordCommand(Guid.NewGuid(), new ChangePasswordDto("WrongPass", "NewPass")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Invalid password");
    }

    [Fact]
    public async Task ChangePasswordHandler_ShouldSucceed()
    {
        var handler = new ChangePasswordHandler(
            _mockUow.Object,
            _mockPasswordHasher.Object,
            _mockChangePasswordLogger.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash:Pass123", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true, Role = UserRole.User };
        user.BindSession(ProductChannel.Marketplace);
        user.RotateRefreshToken("rt", DateTime.UtcNow.AddDays(1));
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new ChangePasswordCommand(Guid.NewGuid(), new ChangePasswordDto("Pass123", "NewPass123")));

        res.Success.Should().BeTrue();
        user.PasswordHash.Should().Be("hash:NewPass123");
        user.RefreshToken.Should().BeNull();
        user.SessionChannel.Should().BeNull();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
