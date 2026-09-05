using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Identity.Domain.Entities;

namespace ParkingApp.Identity.Application.Interfaces;

/// <summary>
/// Single normative Corporate session mint path (KD-CS-25).
/// Password login: forbidBootstrap=false, preferredCompanyId from DTO.
/// SSO: forbidBootstrap=true, preferredCompanyId = SSO company (required).
/// </summary>
public interface ICorporateSessionIssuer
{
    /// <summary>
    /// Membership selection + mint. Successful mint: GenerateAccessToken + GenerateRefreshToken +
    /// User.RecordLogin + User.BindSession + SaveChanges + AuthTokenDtoFactory.Create.
    /// </summary>
    Task<ApiResponse<CorporateLoginResponseDto>> IssueCorporateSessionAsync(
        User user,
        Guid? preferredCompanyId,
        bool forbidBootstrap,
        CancellationToken cancellationToken = default);
}
