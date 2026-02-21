using BCrypt.Net;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Application.CQRS.Commands.Auth;

// ────────────────────────────────────────────────────────────────
// Commands
// ────────────────────────────────────────────────────────────────

public sealed record RegisterCommand(RegisterDto Dto) : ICommand<ApiResponse<TokenDto>>;
public sealed record LoginCommand(LoginDto Dto) : ICommand<ApiResponse<TokenDto>>;
public sealed record RefreshTokenCommand(RefreshTokenDto Dto) : ICommand<ApiResponse<TokenDto>>;
public sealed record LogoutCommand(Guid UserId) : ICommand<ApiResponse<bool>>;
public sealed record ChangePasswordCommand(Guid UserId, ChangePasswordDto Dto) : ICommand<ApiResponse<bool>>;

// ────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────

public sealed class RegisterHandler : ICommandHandler<RegisterCommand, ApiResponse<TokenDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RegisterHandler> _logger;
    private const int RefreshTokenExpirationDays = 7;

    public RegisterHandler(IUnitOfWork unitOfWork, ITokenService tokenService, ILogger<RegisterHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<ApiResponse<TokenDto>> HandleAsync(RegisterCommand command, CancellationToken cancellationToken = default)
    {
        var existingUser = await _unitOfWork.Users.GetByEmailAsync(command.Dto.Email, cancellationToken);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration failed: Email {Email} already exists", command.Dto.Email);
            return new ApiResponse<TokenDto>(false, "Email already registered", null, new List<string> { "Email already exists" });
        }

        var user = new User
        {
            Email = command.Dto.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.Dto.Password),
            FirstName = command.Dto.FirstName.Trim(),
            LastName = command.Dto.LastName.Trim(),
            PhoneNumber = command.Dto.PhoneNumber.Trim(),
            Role = command.Dto.Role
        };

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays);

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User registered: {Email}, Role: {Role}", user.Email, user.Role);

        return new ApiResponse<TokenDto>(true, "Registration successful",
            new TokenDto(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(15), user.ToDto()));
    }
}

public sealed class LoginHandler : ICommandHandler<LoginCommand, ApiResponse<TokenDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginHandler> _logger;
    private const int RefreshTokenExpirationDays = 7;

    public LoginHandler(IUnitOfWork unitOfWork, ITokenService tokenService, ILogger<LoginHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<ApiResponse<TokenDto>> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(command.Dto.Email.ToLower().Trim(), cancellationToken);
        if (user == null || !BCrypt.Net.BCrypt.Verify(command.Dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for email: {Email}", command.Dto.Email);
            return new ApiResponse<TokenDto>(false, "Invalid credentials", null, new List<string> { "Invalid email or password" });
        }

        if (!user.IsActive)
            return new ApiResponse<TokenDto>(false, "Account disabled", null, new List<string> { "Your account has been disabled" });

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays);
        user.LastLoginAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged in: {Email}, UserId: {UserId}", user.Email, user.Id);

        return new ApiResponse<TokenDto>(true, "Login successful",
            new TokenDto(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(15), user.ToDto()));
    }
}

public sealed class RefreshTokenHandler : ICommandHandler<RefreshTokenCommand, ApiResponse<TokenDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private const int RefreshTokenExpirationDays = 7;

    public RefreshTokenHandler(IUnitOfWork unitOfWork, ITokenService tokenService)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
    }

    public async Task<ApiResponse<TokenDto>> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByRefreshTokenAsync(command.Dto.RefreshToken, cancellationToken);
        if (user == null || !_tokenService.ValidateRefreshToken(user, command.Dto.RefreshToken))
            return new ApiResponse<TokenDto>(false, "Invalid refresh token", null, new List<string> { "Refresh token is invalid or expired" });

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays);

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<TokenDto>(true, "Token refreshed",
            new TokenDto(accessToken, refreshToken, DateTime.UtcNow.AddMinutes(15), user.ToDto()));
    }
}

public sealed class LogoutHandler : ICommandHandler<LogoutCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(IUnitOfWork unitOfWork, ILogger<LogoutHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null) return new ApiResponse<bool>(false, "User not found", false);

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged out: {UserId}", command.UserId);
        return new ApiResponse<bool>(true, "Logged out successfully", true);
    }
}

public sealed class ChangePasswordHandler : ICommandHandler<ChangePasswordCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChangePasswordHandler> _logger;

    public ChangePasswordHandler(IUnitOfWork unitOfWork, ILogger<ChangePasswordHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null) return new ApiResponse<bool>(false, "User not found", false);

        if (!BCrypt.Net.BCrypt.Verify(command.Dto.CurrentPassword, user.PasswordHash))
            return new ApiResponse<bool>(false, "Invalid password", false, new List<string> { "Current password is incorrect" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.Dto.NewPassword);
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed for user: {UserId}", command.UserId);
        return new ApiResponse<bool>(true, "Password changed successfully", true);
    }
}
