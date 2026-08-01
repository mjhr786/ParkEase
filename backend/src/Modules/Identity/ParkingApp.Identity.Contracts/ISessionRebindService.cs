namespace ParkingApp.Identity.Contracts;

/// <summary>
/// Host-facing session re-mint (KD-16a). Corporate.Application must not reference Identity.Domain;
/// API controllers call this after CreateCompany (or other bind events).
/// </summary>
public interface ISessionRebindService
{
    /// <summary>
    /// Updates User.Session* and rotates refresh; mints a new access token for the bind.
    /// </summary>
    /// <param name="channel">Marketplace | Corporate | Admin (enum name).</param>
    /// <param name="companyRole">Admin | Employee when Corporate + company bound; otherwise null.</param>
    Task<SessionRebindResult?> RebindAndMintAsync(
        Guid userId,
        string channel,
        Guid? companyId = null,
        string? companyRole = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Primitive token payload for host composition (no Application DTO dependency).</summary>
public sealed class SessionRebindResult
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string Channel { get; init; }
    public Guid? CompanyId { get; init; }
    public string? CompanyRole { get; init; }
    public bool? IsBootstrap { get; init; }

    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string PhoneNumber { get; init; }
    public required string PlatformRole { get; init; }
    public required bool IsEmailVerified { get; init; }
    public required bool IsPhoneVerified { get; init; }
    public required DateTime UserCreatedAt { get; init; }
}
