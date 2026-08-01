using System.ComponentModel.DataAnnotations;
using ParkingApp.Identity.Domain.Enums;
using UserRole = ParkingApp.Identity.Domain.Enums.UserRole;

namespace ParkingApp.Identity.Application.DTOs;

// Auth DTOs
public record RegisterDto(
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)] string Password,
    [Required] string FirstName,
    [Required] string LastName,
    [Required] string PhoneNumber
);

public record LoginDto(
    [Required][EmailAddress] string Email,
    [Required] string Password
);

/// <summary>
/// Property-init record (KD-24) so channel/session fields can grow without positional churn.
/// </summary>
public record TokenDto
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required UserDto User { get; init; }

    /// <summary>Product channel name: Marketplace | Corporate | Admin.</summary>
    public required string Channel { get; init; }

    public Guid? CompanyId { get; init; }
    public string? CompanyRole { get; init; }

    /// <summary>True when Corporate channel without company_id (founder bootstrap).</summary>
    public bool? IsBootstrap { get; init; }
}

/// <summary>
/// Refresh body. Omitted or null <see cref="Channel"/> means preserve server session bind (C5).
/// Non-null channel requests a re-bind (full membership validation in PR3).
/// </summary>
public record RefreshTokenDto(
    [Required] string RefreshToken,
    string? Channel = null,
    Guid? CompanyId = null
);

public record ChangePasswordDto(
    [Required] string CurrentPassword,
    [Required][MinLength(8)] string NewPassword
);

// User DTOs
public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    UserRole Role,
    bool IsEmailVerified,
    bool IsPhoneVerified,
    DateTime CreatedAt
);

public record UpdateUserDto(
    string? FirstName,
    string? LastName,
    string? PhoneNumber
);
