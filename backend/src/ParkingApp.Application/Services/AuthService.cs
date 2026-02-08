using BCrypt.Net;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using ParkingApp.BuildingBlocks.Logging;

namespace ParkingApp.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;
    private const int RefreshTokenExpirationDays = 7;

    public AuthService(IUnitOfWork unitOfWork, ITokenService tokenService, ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<ApiResponse<TokenDto>> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default)
    {
        // Check if email already exists
        var existingUser = await _unitOfWork.Users.GetByEmailAsync(dto.Email, cancellationToken);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration failed: Email {Email} already exists", dto.Email);
            return new ApiResponse<TokenDto>(false, "Email already registered", null, new List<string> { "Email already exists" });
        }

        // Create new user
        var user = new User
        {
            Email = dto.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            Role = dto.Role
        };

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays);

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var tokenDto = new TokenDto(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(15),
            user.ToDto()
        );
        
        _logger.LogInformation("User registered successfully: {Email}, Role: {Role}", user.Email, user.Role);

        return new ApiResponse<TokenDto>(true, "Registration successful", tokenDto);
    }

    public async Task<ApiResponse<TokenDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(dto.Email.ToLower().Trim(), cancellationToken);
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for email: {Email}", dto.Email);
            return new ApiResponse<TokenDto>(false, "Invalid credentials", null, new List<string> { "Invalid email or password" });
        }

        if (!user.IsActive)
        {
            return new ApiResponse<TokenDto>(false, "Account disabled", null, new List<string> { "Your account has been disabled" });
        }

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays);
        user.LastLoginAt = DateTime.UtcNow;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var tokenDto = new TokenDto(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(15),
            user.ToDto()
        );
        
        _logger.LogInformation("User logged in: {Email}, UserId: {UserId}", user.Email, user.Id);

        return new ApiResponse<TokenDto>(true, "Login successful", tokenDto);
    }

    public async Task<ApiResponse<TokenDto>> RefreshTokenAsync(RefreshTokenDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByRefreshTokenAsync(dto.RefreshToken, cancellationToken);

        if (user == null || !_tokenService.ValidateRefreshToken(user, dto.RefreshToken))
        {
            return new ApiResponse<TokenDto>(false, "Invalid refresh token", null, new List<string> { "Refresh token is invalid or expired" });
        }

        // Generate new tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays);

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var tokenDto = new TokenDto(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(15),
            user.ToDto()
        );

        return new ApiResponse<TokenDto>(true, "Token refreshed", tokenDto);
    }

    public async Task<ApiResponse<bool>> LogoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return new ApiResponse<bool>(false, "User not found", false);
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("User logged out: {UserId}", userId);

        return new ApiResponse<bool>(true, "Logged out successfully", true);
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return new ApiResponse<bool>(false, "User not found", false);
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
        {
            return new ApiResponse<bool>(false, "Invalid password", false, new List<string> { "Current password is incorrect" });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.RefreshToken = null; // Invalidate all sessions
        user.RefreshTokenExpiryTime = null;

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Password changed for user: {UserId}", userId);

        return new ApiResponse<bool>(true, "Password changed successfully", true);
    }
}

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<UserService> _logger;
    private static readonly TimeSpan UserCacheDuration = TimeSpan.FromMinutes(10);

    public UserService(IUnitOfWork unitOfWork, ICacheService cache, ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Try cache first
        var cacheKey = $"user:{id}";
        var cached = await _cache.GetAsync<UserDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return new ApiResponse<UserDto>(true, null, cached);
        }

        // Cache miss - fetch from DB
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return new ApiResponse<UserDto>(false, "User not found", null);
        }

        var dto = user.ToDto();
        
        // Cache the result
        await _cache.SetAsync(cacheKey, dto, UserCacheDuration, cancellationToken);

        return new ApiResponse<UserDto>(true, null, dto);
    }

    public async Task<ApiResponse<UserDto>> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return new ApiResponse<UserDto>(false, "User not found", null);
        }

        if (!string.IsNullOrEmpty(dto.FirstName))
            user.FirstName = dto.FirstName.Trim();
        if (!string.IsNullOrEmpty(dto.LastName))
            user.LastName = dto.LastName.Trim();
        if (!string.IsNullOrEmpty(dto.PhoneNumber))
            user.PhoneNumber = dto.PhoneNumber.Trim();

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cache.RemoveAsync($"user:{id}", cancellationToken);
        
        _logger.LogInformation("User profile updated: {UserId}", id);

        return new ApiResponse<UserDto>(true, "Profile updated", user.ToDto());
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return new ApiResponse<bool>(false, "User not found", false);
        }

        _unitOfWork.Users.Remove(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cache.RemoveAsync($"user:{id}", cancellationToken);
        
        _logger.LogWarning("User account deleted: {UserId}", id);

        return new ApiResponse<bool>(true, "Account deleted", true);
    }
}
