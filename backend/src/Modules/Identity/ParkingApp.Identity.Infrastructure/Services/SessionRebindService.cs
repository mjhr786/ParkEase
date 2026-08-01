using ParkingApp.BuildingBlocks.Security;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Identity.Contracts;
using ParkingApp.Identity.Domain.Interfaces;

namespace ParkingApp.Identity.Infrastructure.Services;

/// <summary>
/// Identity UoW session bind + token mint for host orchestration (KD-16a).
/// </summary>
internal sealed class SessionRebindService : ISessionRebindService
{
    private const int RefreshTokenExpirationDays = 7;
    private const int AccessTokenExpirationMinutes = 15;

    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;

    public SessionRebindService(IIdentityUnitOfWork unitOfWork, ITokenService tokenService)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
    }

    public async Task<SessionRebindResult?> RebindAndMintAsync(
        Guid userId,
        string channel,
        Guid? companyId = null,
        string? companyRole = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseChannel(channel, out var productChannel))
            return null;

        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
            return null;

        if (productChannel == ProductChannel.Admin && user.Role != Domain.Enums.UserRole.Admin)
            return null;

        Guid? bindCompanyId = productChannel == ProductChannel.Corporate ? companyId : null;
        string? bindCompanyRole = productChannel == ProductChannel.Corporate ? companyRole : null;

        var accessToken = _tokenService.GenerateAccessToken(user, productChannel, bindCompanyId, bindCompanyRole);
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RotateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(RefreshTokenExpirationDays));
        user.BindSession(productChannel, bindCompanyId, bindCompanyRole);

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SessionRebindResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(AccessTokenExpirationMinutes),
            Channel = productChannel.ToString(),
            CompanyId = bindCompanyId,
            CompanyRole = bindCompanyRole,
            IsBootstrap = productChannel == ProductChannel.Corporate ? bindCompanyId is null : null,
            UserId = user.Id,
            Email = user.Email?.Value ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            PlatformRole = user.Role.ToString(),
            IsEmailVerified = user.IsEmailVerified,
            IsPhoneVerified = user.IsPhoneVerified,
            UserCreatedAt = user.CreatedAt
        };
    }

    private static bool TryParseChannel(string? value, out ProductChannel channel)
    {
        channel = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (int.TryParse(trimmed, out _))
            return false;

        foreach (var name in Enum.GetNames<ProductChannel>())
        {
            if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse(name, ignoreCase: false, out channel)
                && Enum.IsDefined(channel))
            {
                return true;
            }
        }

        return false;
    }
}
