using ParkingApp.BuildingBlocks.Security;
using ParkingApp.Identity.Domain.Entities;

namespace ParkingApp.Identity.Application.Interfaces;

/// <summary>
/// Application port for JWT / refresh-token issuance and validation.
/// Implemented in Infrastructure.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Mint access token with product channel claim (+ optional corporate company_id / company_role).
    /// </summary>
    string GenerateAccessToken(
        User user,
        ProductChannel channel,
        Guid? companyId = null,
        string? companyRole = null);

    string GenerateRefreshToken();
    bool ValidateRefreshToken(User user, string refreshToken);
}
