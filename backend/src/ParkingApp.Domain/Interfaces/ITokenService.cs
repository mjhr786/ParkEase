using ParkingApp.Domain.Entities;

namespace ParkingApp.Domain.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    bool ValidateRefreshToken(User user, string refreshToken);
}
