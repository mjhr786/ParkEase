using ParkingApp.Identity.Domain.Enums;

namespace ParkingApp.Identity.Application.DTOs;

public sealed record AdminUserListItemDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    UserRole Role,
    bool IsActive,
    bool IsEmailVerified,
    DateTime? LastLoginAt,
    DateTime CreatedAt);

public sealed record AdminUserDetailDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    UserRole Role,
    bool IsActive,
    bool IsEmailVerified,
    bool IsPhoneVerified,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    int VehicleCount);

public sealed record AdminUserPageDto(
    IReadOnlyList<AdminUserListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record AdminSetUserActiveRequest(string Reason);
