using System;
using System.IdentityModel.Tokens.Jwt;
using ParkingApp.Identity.Infrastructure.Services;
using System.Linq;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ParkingApp.BuildingBlocks.Security;
using ParkingApp.Identity.Domain.Entities;
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
    public void GenerateAccessToken_ShouldReturnValidToken_WithChannelClaim()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };

        var token = _service.GenerateAccessToken(user, ProductChannel.Marketplace);

        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be("TestIssuer");
        jwtToken.Audiences.Should().Contain("TestAudience");
        jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value.Should().Be(user.Id.ToString());
        jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value.Should().Be(user.Email.Value);
        jwtToken.Claims.First(c => c.Type == ClaimTypes.Role).Value.Should().Be(user.Role.ToString());
        jwtToken.Claims.First(c => c.Type == ParkEaseClaimTypes.Channel).Value.Should().Be(nameof(ProductChannel.Marketplace));
        jwtToken.Claims.Any(c => c.Type == ParkEaseClaimTypes.CompanyId).Should().BeFalse();
        jwtToken.Claims.Any(c => c.Type == ParkEaseClaimTypes.CompanyRole).Should().BeFalse();
    }

    [Fact]
    public void GenerateAccessToken_Corporate_ShouldIncludeCompanyClaims()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "corp@example.com", PasswordHash = "hash", FirstName = "Corp", LastName = "User", PhoneNumber = "1", IsActive = true };
        var companyId = Guid.NewGuid();

        var token = _service.GenerateAccessToken(user, ProductChannel.Corporate, companyId, "Admin");
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwtToken.Claims.First(c => c.Type == ParkEaseClaimTypes.Channel).Value.Should().Be(nameof(ProductChannel.Corporate));
        jwtToken.Claims.First(c => c.Type == ParkEaseClaimTypes.CompanyId).Value.Should().Be(companyId.ToString());
        jwtToken.Claims.First(c => c.Type == ParkEaseClaimTypes.CompanyRole).Value.Should().Be("Admin");
    }

    [Fact]
    public void GenerateAccessToken_AdminChannel_ShouldEmitAdminClaim()
    {
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

        var token = _service.GenerateAccessToken(user, ProductChannel.Admin);
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwtToken.Claims.First(c => c.Type == ParkEaseClaimTypes.Channel).Value.Should().Be(nameof(ProductChannel.Admin));
        jwtToken.Claims.First(c => c.Type == ClaimTypes.Role).Value.Should().Be(nameof(UserRole.Admin));
    }

    [Fact]
    public void GenerateAccessToken_Marketplace_IgnoresCompanyArgs()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "m@example.com", PasswordHash = "hash", FirstName = "M", LastName = "U", PhoneNumber = "1", IsActive = true };
        var companyId = Guid.NewGuid();

        var token = _service.GenerateAccessToken(user, ProductChannel.Marketplace, companyId, "Admin");
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwtToken.Claims.First(c => c.Type == ParkEaseClaimTypes.Channel).Value.Should().Be(nameof(ProductChannel.Marketplace));
        jwtToken.Claims.Any(c => c.Type == ParkEaseClaimTypes.CompanyId).Should().BeFalse();
        jwtToken.Claims.Any(c => c.Type == ParkEaseClaimTypes.CompanyRole).Should().BeFalse();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnString()
    {
        var token = _service.GenerateRefreshToken();
        token.Should().NotBeNullOrEmpty();
        token.Length.Should().BeGreaterThan(20);
    }

    [Fact]
    public void ValidateRefreshToken_ShouldReturnTrue_WhenValid()
    {
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
        _service.ValidateRefreshToken(user, token).Should().BeTrue();
    }

    [Fact]
    public void ValidateRefreshToken_ShouldReturnFalse_WhenExpired()
    {
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
        _service.ValidateRefreshToken(user, token).Should().BeFalse();
    }

    [Fact]
    public void ValidateRefreshToken_ShouldReturnFalse_WhenTokenMismatch()
    {
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
        _service.ValidateRefreshToken(user, "wrong-token").Should().BeFalse();
    }
}
