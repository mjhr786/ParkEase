using Microsoft.EntityFrameworkCore;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;

namespace ParkingApp.Corporate.Infrastructure.Services;

public sealed class CompanySsoAdminStore : ICompanySsoAdminStore
{
    private static readonly string[] LoginSuccessActions =
    [
        "sso.complete",
        "Complete",
        "CallbackSuccess",
        "sso.callback.success"
    ];

    private readonly ICorporateDbContext _db;

    public CompanySsoAdminStore(ICorporateDbContext db)
    {
        _db = db;
    }

    public async Task<CompanySsoConfiguration?> GetTrackedWithDomainsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await _db.CompanySsoConfigurations
            .Include(c => c.Domains)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && !c.IsDeleted, cancellationToken);
    }

    public async Task AddAsync(CompanySsoConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await _db.CompanySsoConfigurations.AddAsync(configuration, cancellationToken);
    }

    public async Task<bool> IsDomainVerifiedByOtherCompanyAsync(
        string normalizedDomain,
        Guid excludeCompanyId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedDomain))
            return false;

        return await _db.CompanySsoDomains
            .AsNoTracking()
            .AnyAsync(d =>
                !d.IsDeleted
                && d.IsVerified
                && d.Domain == normalizedDomain
                && d.CompanyId != excludeCompanyId, cancellationToken);
    }

    public async Task WriteAuditAsync(CorporateSsoAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await _db.CorporateSsoAuditEvents.AddAsync(auditEvent, cancellationToken);
    }

    public async Task<IReadOnlyList<CorporateSsoAuditEvent>> GetRecentAuditAsync(
        Guid companyId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await _db.CorporateSsoAuditEvents
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<PlatformCompanySsoListRow> Items, int TotalCount)> ListPlatformSsoConfigsAsync(
        string? search,
        bool? forceDisabledOnly,
        bool? enabledOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var q =
            from cfg in _db.CompanySsoConfigurations.AsNoTracking()
            join company in _db.Companies.AsNoTracking() on cfg.CompanyId equals company.Id
            where !cfg.IsDeleted && !company.IsDeleted
            select new { cfg, company };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(x =>
                x.company.Name.ToLower().Contains(term)
                || (x.company.Slug != null && x.company.Slug.ToLower().Contains(term))
                || (x.cfg.Authority != null && x.cfg.Authority.ToLower().Contains(term)));
        }

        if (forceDisabledOnly == true)
            q = q.Where(x => x.cfg.ForceDisabledByPlatform);

        if (enabledOnly == true)
            q = q.Where(x => x.cfg.IsEnabled && !x.cfg.ForceDisabledByPlatform);

        var total = await q.CountAsync(cancellationToken);

        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var pageRows = await q
            .OrderByDescending(x => x.cfg.ForceDisabledByPlatform)
            .ThenByDescending(x => x.cfg.IsEnabled)
            .ThenBy(x => x.company.Name)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new
            {
                x.cfg.CompanyId,
                CompanyName = x.company.Name,
                CompanySlug = x.company.Slug,
                CompanyIsActive = x.company.IsActive,
                x.cfg.IsEnabled,
                x.cfg.ForceDisabledByPlatform,
                x.cfg.ForceDisabledAt,
                x.cfg.ForceDisabledByUserId,
                Protocol = x.cfg.Protocol.ToString(),
                Authority = x.cfg.Authority,
                x.cfg.LastTestedAt,
                x.cfg.LastTestResult,
                VerifiedDomainCount = x.cfg.Domains.Count(d => !d.IsDeleted && d.IsVerified),
                x.cfg.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var items = pageRows
            .Select(x => new PlatformCompanySsoListRow(
                x.CompanyId,
                x.CompanyName,
                x.CompanySlug,
                x.CompanyIsActive,
                x.IsEnabled,
                x.ForceDisabledByPlatform,
                x.ForceDisabledAt,
                x.ForceDisabledByUserId,
                x.Protocol,
                string.IsNullOrWhiteSpace(x.Authority) ? null : x.Authority,
                x.LastTestedAt,
                x.LastTestResult,
                x.VerifiedDomainCount,
                x.UpdatedAt))
            .ToList();

        return (items, total);
    }

    public async Task<int> CountSuccessfulSsoLoginsAsync(
        Guid companyId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        return await _db.CorporateSsoAuditEvents
            .AsNoTracking()
            .CountAsync(e =>
                e.CompanyId == companyId
                && !e.IsDeleted
                && e.CreatedAt >= sinceUtc
                && e.Outcome == "success"
                && LoginSuccessActions.Contains(e.Action), cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountSuccessfulSsoLoginsByCompanyAsync(
        IReadOnlyCollection<Guid> companyIds,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        if (companyIds.Count == 0)
            return new Dictionary<Guid, int>();

        var ids = companyIds.Distinct().ToList();
        var rows = await _db.CorporateSsoAuditEvents
            .AsNoTracking()
            .Where(e =>
                e.CompanyId != null
                && ids.Contains(e.CompanyId.Value)
                && !e.IsDeleted
                && e.CreatedAt >= sinceUtc
                && e.Outcome == "success"
                && LoginSuccessActions.Contains(e.Action))
            .GroupBy(e => e.CompanyId!.Value)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.CompanyId, r => r.Count);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
