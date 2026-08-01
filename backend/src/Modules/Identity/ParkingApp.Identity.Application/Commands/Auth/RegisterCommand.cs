using ParkingApp.Application.CQRS;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using TokenDto = ParkingApp.Identity.Application.DTOs.TokenDto;

using ParkingApp.BuildingBlocks.Security;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Enums;
using ParkingApp.Identity.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Identity.Application.Commands.Auth;

// ═══════════════════════════════════════════════════════════════════════════════
// Commands
// ═══════════════════════════════════════════════════════════════════════════════

public sealed record RegisterCommand(RegisterDto Dto) : ICommand<ApiResponse<TokenDto>>;
public sealed record LoginCommand(LoginDto Dto) : ICommand<ApiResponse<TokenDto>>;
public sealed record RefreshTokenCommand(RefreshTokenDto Dto) : ICommand<ApiResponse<TokenDto>>;
public sealed record LogoutCommand(Guid UserId) : ICommand<ApiResponse<bool>>;
public sealed record ChangePasswordCommand(Guid UserId, ChangePasswordDto Dto) : ICommand<ApiResponse<bool>>;

// ═══════════════════════════════════════════════════════════════════════════════
// Handlers
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed class RegisterHandler : ICommandHandler<RegisterCommand, ApiResponse<TokenDto>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<RegisterHandler> _logger;
    private const int RefreshTokenExpirationDays = 7;

    public RegisterHandler(
        IIdentityUnitOfWork unitOfWork,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        ILogger<RegisterHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<ApiResponse<TokenDto>> HandleAsync(RegisterCommand command, CancellationToken cancellationToken = default)
    {
        var existingUser = await _unitOfWork.Users.GetByEmailAsync(command.Dto.Email.ToLower().Trim(), cancellationToken);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration failed: Email {Email} already exists", command.Dto.Email);
            return new ApiResponse<TokenDto>(false, "Email already registered", null, new List<string> { "Email already exists" });
        }

        var user = User.Register(
            command.Dto.Email,
            _passwordHasher.Hash(command.Dto.Password),
            command.Dto.FirstName,
            command.Dto.LastName,
            command.Dto.PhoneNumber);

        // KD-3: register always mints Marketplace
        var channel = ProductChannel.Marketplace;
        var accessToken = _tokenService.GenerateAccessToken(user, channel);
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RotateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(RefreshTokenExpirationDays));
        user.BindSession(channel);

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User registered: {Email}, Role: {Role}", user.Email, user.Role);

        return new ApiResponse<TokenDto>(true, "Registration successful",
            AuthTokenDtoFactory.Create(accessToken, refreshToken, user, channel));
    }
}

internal sealed class LoginHandler : ICommandHandler<LoginCommand, ApiResponse<TokenDto>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<LoginHandler> _logger;
    private const int RefreshTokenExpirationDays = 7;

    public LoginHandler(
        IIdentityUnitOfWork unitOfWork,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        ILogger<LoginHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<ApiResponse<TokenDto>> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(command.Dto.Email.ToLower().Trim(), cancellationToken);
        if (user == null || !_passwordHasher.Verify(command.Dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for email: {Email}", command.Dto.Email);
            return new ApiResponse<TokenDto>(false, "Invalid credentials", null, new List<string> { "Invalid email or password" });
        }

        if (!user.IsActive)
            return new ApiResponse<TokenDto>(false, "Account disabled", null, new List<string> { "Your account has been disabled" });

        // KD-3: default login → Marketplace for User; Admin for UserRole.Admin. Corporate is PR3.
        var channel = user.Role == UserRole.Admin ? ProductChannel.Admin : ProductChannel.Marketplace;
        var accessToken = _tokenService.GenerateAccessToken(user, channel);
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RecordLogin(refreshToken, DateTime.UtcNow.AddDays(RefreshTokenExpirationDays));
        user.BindSession(channel);

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged in: {Email}, UserId: {UserId}, Channel: {Channel}", user.Email, user.Id, channel);

        return new ApiResponse<TokenDto>(true, "Login successful",
            AuthTokenDtoFactory.Create(accessToken, refreshToken, user, channel));
    }
}

internal sealed class RefreshTokenHandler : ICommandHandler<RefreshTokenCommand, ApiResponse<TokenDto>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private const int RefreshTokenExpirationDays = 7;

    public RefreshTokenHandler(IIdentityUnitOfWork unitOfWork, ITokenService tokenService)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
    }

    public async Task<ApiResponse<TokenDto>> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByRefreshTokenAsync(command.Dto.RefreshToken, cancellationToken);
        if (user == null || !_tokenService.ValidateRefreshToken(user, command.Dto.RefreshToken))
            return new ApiResponse<TokenDto>(false, "Invalid refresh token", null, new List<string> { "Refresh token is invalid or expired" });

        // C5 / KD-2 refresh algorithm:
        // 1. Non-null channel in body → limited re-bind (PR1: Marketplace demotion; Admin if role; Corporate deferred to PR3).
        // 2. Null / omit → use User.Session*.
        // 3. Session null (legacy) → Marketplace (or Admin by role), then persist.
        ProductChannel channel;
        Guid? companyId;
        string? companyRole;

        if (!string.IsNullOrWhiteSpace(command.Dto.Channel))
        {
            if (!TryParseChannelName(command.Dto.Channel, out channel))
            {
                return new ApiResponse<TokenDto>(
                    false,
                    "Invalid channel",
                    null,
                    new List<string> { "Channel must be Marketplace, Corporate, or Admin" },
                    "invalid_channel");
            }

            switch (channel)
            {
                case ProductChannel.Marketplace:
                    // Demotion / marketplace re-bind: clear company session fields.
                    companyId = null;
                    companyRole = null;
                    break;

                case ProductChannel.Admin:
                    if (user.Role != UserRole.Admin)
                    {
                        return new ApiResponse<TokenDto>(
                            false,
                            "Admin channel requires Admin role",
                            null,
                            new List<string> { "Admin channel re-bind is not allowed for this user" },
                            "channel_rebind_forbidden");
                    }
                    companyId = null;
                    companyRole = null;
                    break;

                case ProductChannel.Corporate:
                    // Full membership validation lands in PR3 — do not poison Session* with unvalidated companyId.
                    return new ApiResponse<TokenDto>(
                        false,
                        "Corporate channel re-bind is not available yet",
                        null,
                        new List<string> { "Use corporate login/switch (PR3); refresh may only preserve an existing Corporate session when channel is omitted or null" },
                        "channel_rebind_forbidden");

                default:
                    return new ApiResponse<TokenDto>(
                        false,
                        "Invalid channel",
                        null,
                        new List<string> { "Channel must be Marketplace, Corporate, or Admin" },
                        "invalid_channel");
            }
        }
        else
        {
            // Omitted or explicit null → preserve server session bind
            if (user.SessionChannel is null)
            {
                channel = user.Role == UserRole.Admin ? ProductChannel.Admin : ProductChannel.Marketplace;
                companyId = null;
                companyRole = null;
            }
            else
            {
                channel = user.SessionChannel.Value;
                companyId = user.SessionCompanyId;
                companyRole = user.SessionCompanyRole;
            }
        }

        var accessToken = _tokenService.GenerateAccessToken(user, channel, companyId, companyRole);
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RotateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(RefreshTokenExpirationDays));
        user.BindSession(channel, companyId, companyRole);

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<TokenDto>(true, "Token refreshed",
            AuthTokenDtoFactory.Create(accessToken, refreshToken, user, channel, companyId, companyRole));
    }

    /// <summary>
    /// Accept only named enum values (Marketplace|Corporate|Admin), case-insensitive.
    /// Rejects numeric enum strings ("1","2","3") and unknown names.
    /// </summary>
    internal static bool TryParseChannelName(string? value, out ProductChannel channel)
    {
        channel = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        // Reject pure numeric (Enum.TryParse would accept "1"/"2"/"3")
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

internal sealed class LogoutHandler : ICommandHandler<LogoutCommand, ApiResponse<bool>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(IIdentityUnitOfWork unitOfWork, ILogger<LogoutHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null) return new ApiResponse<bool>(false, "User not found", false);

        user.RevokeRefreshToken();
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged out: {UserId}", command.UserId);
        return new ApiResponse<bool>(true, "Logged out successfully", true);
    }
}

internal sealed class ChangePasswordHandler : ICommandHandler<ChangePasswordCommand, ApiResponse<bool>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ChangePasswordHandler> _logger;

    public ChangePasswordHandler(
        IIdentityUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ILogger<ChangePasswordHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null) return new ApiResponse<bool>(false, "User not found", false);

        if (!_passwordHasher.Verify(command.Dto.CurrentPassword, user.PasswordHash))
            return new ApiResponse<bool>(false, "Invalid password", false, new List<string> { "Current password is incorrect" });

        user.ChangePassword(_passwordHasher.Hash(command.Dto.NewPassword));

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed for user: {UserId}", command.UserId);
        return new ApiResponse<bool>(true, "Password changed successfully", true);
    }
}
