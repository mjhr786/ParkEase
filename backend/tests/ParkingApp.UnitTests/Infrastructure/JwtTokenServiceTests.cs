using ParkingApp.Infrastructure.Services;
using System;
using System.IdentityModel.Tokens.Jwt;
using ParkingApp.Identity.Infrastructure.Services;
using System.Linq;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Identity.Domain.Enums;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure;

public class JwtTokenServiceTests
{
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly JwtTokenService _service;
    private const string SecretKey = "super-secret-key-that-is-at-least-32-characters";

    public JwtTokenServiceTests()
    {
        _mockConfig = new Mock<IConfiguration>();
        _mockConfig.Setup(c => c["Jwt:SecretKey"]).Returns(SecretKey);
        _mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
        _mockConfig.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
        _mockConfig.Setup(c => c["Jwt:AccessTokenExpirationMinutes"]).Returns("60");

        _service = new JwtTokenService(_mockConfig.Object);
    }

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidToken()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be("TestIssuer");
        jwtToken.Audiences.Should().Contain("TestAudience");
        jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value.Should().Be(user.Id.ToString());
        jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value.Should().Be(user.Email.Value);
        jwtToken.Claims.First(c => c.Type == ClaimTypes.Role).Value.Should().Be(user.Role.ToString());
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnString()
    {
        // Act
        var token = _service.GenerateRefreshToken();

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Length.Should().BeGreaterThan(20);
    }

    [Fact]
    public void ValidateRefreshToken_ShouldReturnTrue_WhenValid()
    {
        // Arrange
        var token = "valid-token";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "1",
            IsActive = true,
            RefreshToken = token,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddHours(1)
        };

        // Act
        var isValid = _service.ValidateRefreshToken(user, token);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateRefreshToken_ShouldReturnFalse_WhenExpired()
    {
        // Arrange
        var token = "valid-token";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "1",
            IsActive = true,
            RefreshToken = token,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddHours(-1)
        };

        // Act
        var isValid = _service.ValidateRefreshToken(user, token);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateRefreshToken_ShouldReturnFalse_WhenTokenMismatch()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "1",
            IsActive = true,
            RefreshToken = "valid-token",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddHours(1)
        };

        // Act
        var isValid = _service.ValidateRefreshToken(user, "wrong-token");

        // Assert
        isValid.Should().BeFalse();
    }
}





