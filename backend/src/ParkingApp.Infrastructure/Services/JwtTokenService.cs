using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Infrastructure.Services;

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpirationMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        _secretKey = _configuration["Jwt:SecretKey"] ?? throw new ArgumentNullException("Jwt:SecretKey is not configured");
        _issuer = _configuration["Jwt:Issuer"] ?? "ParkingApp";
        _audience = _configuration["Jwt:Audience"] ?? "ParkingApp";
        _accessTokenExpirationMinutes = int.Parse(_configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15");
    }

    public string GenerateAccessToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("firstName", user.FirstName),
            new Claim("lastName", user.LastName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public bool ValidateRefreshToken(User user, string refreshToken)
    {
        if (string.IsNullOrEmpty(user.RefreshToken) || user.RefreshToken != refreshToken)
            return false;

        if (user.RefreshTokenExpiryTime == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            return false;

        return true;
    }
}
