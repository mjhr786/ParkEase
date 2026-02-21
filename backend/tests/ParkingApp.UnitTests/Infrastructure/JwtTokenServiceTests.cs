using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Configuration;
using ParkingApp.Infrastructure.Services;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ParkingApp.UnitTests.Infrastructure;

public class JwtTokenServiceTests
{
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly JwtTokenService _service;
    private const string SecretKey = "SuperSecretKeyForTestingDontUseInProduction";

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
    public void GenerateAccessToken_ShouldReturnValidJwt()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = UserRole.Member
        };

        // Act
        var token = _service.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be("TestIssuer");
        jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value.Should().Be(user.Id.ToString());
        jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value.Should().Be(user.Email);
        jwtToken.Claims.First(c => c.Type == ClaimTypes.Role).Value.Should().Be(user.Role.ToString());
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnRandomString()
    {
        // Act
        var token1 = _service.GenerateRefreshToken();
        var token2 = _service.GenerateRefreshToken();

        // Assert
        token1.Should().NotBeNullOrEmpty();
        token2.Should().NotBeNullOrEmpty();
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void ValidateRefreshToken_WithCorrectToken_ShouldReturnTrue()
    {
        // Arrange
        var token = "valid-token";
        var user = new User 
        { 
            RefreshToken = token, 
            RefreshTokenExpiryTime = DateTime.UtcNow.AddHours(1) 
        };

        // Act
        var result = _service.ValidateRefreshToken(user, token);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateRefreshToken_WithExpiredToken_ShouldReturnFalse()
    {
        // Arrange
        var token = "valid-token";
        var user = new User 
        { 
            RefreshToken = token, 
            RefreshTokenExpiryTime = DateTime.UtcNow.AddHours(-1) 
        };

        // Act
        var result = _service.ValidateRefreshToken(user, token);

        // Assert
        result.Should().BeFalse();
    }
}
