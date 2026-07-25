using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.BuildingBlocks.ValueObjects;
using ParkingApp.Identity.Domain.Enums;

namespace ParkingApp.Identity.Domain.Entities;

/// <summary>
/// Identity / user aggregate.
/// Cross-module parties (bookings, parking spaces, etc.) are referenced elsewhere by UserId only.
/// </summary>
public class User : BaseEntity
{
    /// <summary>Validated email value object (persisted as string via EF conversion).</summary>
    public Email Email { get; internal set; } = null!;

    public string PasswordHash { get; internal set; } = string.Empty;
    public string FirstName { get; internal set; } = string.Empty;
    public string LastName { get; internal set; } = string.Empty;
    public string PhoneNumber { get; internal set; } = string.Empty;
    public UserRole Role { get; internal set; }
    public bool IsEmailVerified { get; internal set; }
    public bool IsPhoneVerified { get; internal set; }
    public bool IsActive { get; internal set; } = true;
    public string? RefreshToken { get; internal set; }
    public DateTime? RefreshTokenExpiryTime { get; internal set; }
    public DateTime? LastLoginAt { get; internal set; }

    // Identity-owned collections only (no Marketplace reverse navigations)
    public virtual ICollection<Vehicle> Vehicles { get; internal set; } = new List<Vehicle>();
    public virtual ICollection<DeviceToken> DeviceTokens { get; internal set; } = new List<DeviceToken>();

    public string FullName => $"{FirstName} {LastName}".Trim();

    internal User()
    {
    }

    public static User Register(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string phoneNumber,
        UserRole role = UserRole.User)
    {
        Email emailVo;
        try
        {
            emailVo = new Email(email);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException("email", ex.Message);
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ValidationException("passwordHash", "Password hash is required");
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ValidationException("firstName", "First name is required");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ValidationException("lastName", "Last name is required");

        return new User
        {
            Email = emailVo,
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PhoneNumber = phoneNumber?.Trim() ?? string.Empty,
            Role = role,
            IsActive = true
        };
    }

    public void UpdateProfile(string? firstName, string? lastName, string? phoneNumber)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
            FirstName = firstName.Trim();
        if (!string.IsNullOrWhiteSpace(lastName))
            LastName = lastName.Trim();
        if (!string.IsNullOrWhiteSpace(phoneNumber))
            PhoneNumber = phoneNumber.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ValidationException("passwordHash", "Password hash is required");
        PasswordHash = passwordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RotateRefreshToken(string refreshToken, DateTime expiryUtc)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ValidationException("refreshToken", "Refresh token is required");
        RefreshToken = refreshToken;
        RefreshTokenExpiryTime = expiryUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiryTime = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin(string refreshToken, DateTime refreshTokenExpiryUtc)
    {
        if (!IsActive)
            throw new BusinessRuleException("User.RecordLogin", "Account is disabled");
        RotateRefreshToken(refreshToken, refreshTokenExpiryUtc);
        LastLoginAt = DateTime.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        SetPasswordHash(newPasswordHash);
        RevokeRefreshToken();
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        RevokeRefreshToken();
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkEmailVerified()
    {
        IsEmailVerified = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPhoneVerified()
    {
        IsPhoneVerified = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
