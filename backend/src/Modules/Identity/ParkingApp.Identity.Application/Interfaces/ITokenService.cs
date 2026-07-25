using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Identity.Domain.Entities;

namespace ParkingApp.Identity.Application.Interfaces;

/// <summary>
/// Application port for JWT / refresh-token issuance and validation.
/// Implemented in Infrastructure.
/// </summary>
public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    bool ValidateRefreshToken(User user, string refreshToken);
}

