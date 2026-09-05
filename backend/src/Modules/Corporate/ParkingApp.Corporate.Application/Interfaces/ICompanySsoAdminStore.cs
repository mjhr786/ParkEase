using ParkingApp.Corporate.Domain;

namespace ParkingApp.Corporate.Application.Interfaces;

/// <summary>
/// Tracked SSO config persistence for company admin (PR5) and platform admin (PR6) mutations.
/// </summary>
public interface ICompanySsoAdminStore
{
    Task<CompanySsoConfiguration?> GetTrackedWithDomainsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task AddAsync(CompanySsoConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another company already holds a verified claim on <paramref name="normalizedDomain"/>.
    /// </summary>
    Task<bool> IsDomainVerifiedByOtherCompanyAsync(
        string normalizedDomain,
        Guid excludeCompanyId,
        CancellationToken cancellationToken = default);

    Task WriteAuditAsync(CorporateSsoAuditEvent auditEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorporateSsoAuditEvent>> GetRecentAuditAsync(
        Guid companyId,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Platform list: companies that have an SSO configuration row (enabled, disabled, or force-locked).
    /// </summary>
    Task<(IReadOnlyList<PlatformCompanySsoListRow> Items, int TotalCount)> ListPlatformSsoConfigsAsync(
        string? search,
        bool? forceDisabledOnly,
        bool? enabledOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count successful SSO login audit events for a company in the last <paramref name="sinceUtc"/> window.
    /// </summary>
    Task<int> CountSuccessfulSsoLoginsAsync(
        Guid companyId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Batch version of <see cref="CountSuccessfulSsoLoginsAsync"/> for list pages.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountSuccessfulSsoLoginsByCompanyAsync(
        IReadOnlyCollection<Guid> companyIds,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Read model used by platform SSO list (store → handler).</summary>
public sealed record PlatformCompanySsoListRow(
    Guid CompanyId,
    string CompanyName,
    string? CompanySlug,
    bool CompanyIsActive,
    bool IsEnabled,
    bool ForceDisabledByPlatform,
    DateTime? ForceDisabledAt,
    Guid? ForceDisabledByUserId,
    string Protocol,
    string? Authority,
    DateTime? LastTestedAt,
    string? LastTestResult,
    int VerifiedDomainCount,
    DateTime? UpdatedAt);
