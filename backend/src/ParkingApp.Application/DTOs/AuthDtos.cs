using System.ComponentModel.DataAnnotations;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.DTOs;

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

public record GoogleLoginDto(
    [Required] string GoogleId,
    [Required][EmailAddress] string Email,
    [Required] string FirstName,
    [Required] string LastName,
    string? ProfilePicture,
    string? IdToken,
    string? AccessToken
);

public record TokenDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record RefreshTokenDto(
    [Required] string RefreshToken
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
