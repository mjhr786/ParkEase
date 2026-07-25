namespace ParkingApp.Identity.Contracts;

/// <summary>
/// Cross-module identity read model. No Domain entity types.
/// </summary>
public sealed record UserSummary(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber = null,
    bool IsActive = true,
    bool IsAdmin = false)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}
