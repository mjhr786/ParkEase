using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Identity.Domain.Entities;

namespace ParkingApp.Identity.Application.Mappings;

/// <summary>Identity module mappings.</summary>
public static class IdentityMappings
{
    public static UserDto ToDto(this User user) => new(
        user.Id,
        user.Email?.Value ?? string.Empty,
        user.FirstName,
        user.LastName,
        user.PhoneNumber,
        user.Role,
        user.IsEmailVerified,
        user.IsPhoneVerified,
        user.CreatedAt
    );
}
