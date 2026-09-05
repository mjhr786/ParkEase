using ParkingApp.BuildingBlocks.Persistence;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Enums;

namespace ParkingApp.Identity.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Email lookup including soft-deleted rows. Used for unique-index collision detection
    /// (IX_Users_Email still applies to deleted users).
    /// </summary>
    Task<User?> GetByEmailIncludingDeletedAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find user by external IdP linkage (provider + subject), including the matching ExternalLogin.
    /// </summary>
    Task<User?> GetByExternalLoginAsync(
        ExternalAuthProvider provider,
        string providerSubject,
        CancellationToken cancellationToken = default);

    /// <summary>Platform admin user search with paging (Identity-owned).</summary>
    Task<(IReadOnlyList<User> Items, int TotalCount)> SearchForAdminAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public interface IVehicleRepository : IRepository<Vehicle>
{
    Task<IEnumerable<Vehicle>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetDefaultVehicleAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IDeviceTokenRepository : IRepository<DeviceToken>
{
    Task<IEnumerable<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<DeviceToken?> GetByDeviceIdAndUserIdAsync(string deviceId, Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetFcmTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IUserExternalLoginRepository : IRepository<UserExternalLogin>
{
    Task<UserExternalLogin?> GetByProviderSubjectAsync(
        ExternalAuthProvider provider,
        string providerSubject,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserExternalLogin>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface ICorporateSsoIdentityLinkRepository : IRepository<CorporateSsoIdentityLink>
{
    Task<CorporateSsoIdentityLink?> GetByCompanySubjectAsync(
        Guid companyId,
        SsoProtocol protocol,
        string subject,
        CancellationToken cancellationToken = default);

    Task<CorporateSsoIdentityLink?> GetByCompanyUserAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorporateSsoIdentityLink>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Identity module unit-of-work port (users, vehicles, device tokens, external logins, corporate SSO links).
/// </summary>
public interface IIdentityUnitOfWork : IUnitOfWorkTransaction
{
    IUserRepository Users { get; }
    IVehicleRepository Vehicles { get; }
    IDeviceTokenRepository DeviceTokens { get; }
    IUserExternalLoginRepository ExternalLogins { get; }
    ICorporateSsoIdentityLinkRepository CorporateSsoLinks { get; }
}
