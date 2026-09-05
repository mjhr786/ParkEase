using Microsoft.EntityFrameworkCore;
using ParkingApp.Corporate.Contracts;

namespace ParkingApp.Corporate.Infrastructure.ModuleAdapters;

/// <summary>Cross-module SSO config read model (password gate + discover/start).</summary>
public sealed class CompanySsoConfigLookup : ICompanySsoConfigLookup
{
    private readonly ICorporateDbContext _db;

    public CompanySsoConfigLookup(ICorporateDbContext db)
    {
        _db = db;
    }

    public async Task<CompanySsoConfigSnapshot?> GetByCompanyIdAsync(Guid companyId, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .AsNoTracking()
            .Include(c => c.SsoConfiguration!)
            .ThenInclude(s => s.Domains)
            .FirstOrDefaultAsync(c => c.Id == companyId && !c.IsDeleted, ct);

        if (company is null)
            return null;

        return Map(company);
    }

    public async Task<CompanySsoConfigSnapshot?> GetByVerifiedDomainAsync(string domain, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        var normalized = domain.Trim().ToLowerInvariant().TrimStart('@');
        var company = await _db.Companies
            .AsNoTracking()
            .Include(c => c.SsoConfiguration!)
            .ThenInclude(s => s.Domains)
            .FirstOrDefaultAsync(c =>
                !c.IsDeleted
                && c.SsoConfiguration != null
                && c.SsoConfiguration.Domains.Any(d =>
                    !d.IsDeleted && d.IsVerified && d.Domain == normalized), ct);

        return company is null ? null : Map(company);
    }

    public async Task<CompanySsoConfigSnapshot?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var normalized = slug.Trim().ToLowerInvariant();
        var company = await _db.Companies
            .AsNoTracking()
            .Include(c => c.SsoConfiguration!)
            .ThenInclude(s => s.Domains)
            .FirstOrDefaultAsync(c => !c.IsDeleted && c.Slug == normalized, ct);

        return company is null ? null : Map(company);
    }

    private static CompanySsoConfigSnapshot Map(Domain.Company company)
    {
        var cfg = company.SsoConfiguration;
        var verifiedDomains = cfg?.Domains
            .Where(d => !d.IsDeleted && d.IsVerified)
            .Select(d => d.Domain)
            .ToList() ?? new List<string>();

        var hasVerified = verifiedDomains.Count > 0;
        var isEnabled = cfg?.IsEnabled ?? false;
        var forceDisabled = cfg?.ForceDisabledByPlatform ?? false;
        // Infrastructure half of effective enablement (global flag + allow-list applied by Identity handlers)
        var effectivelyEnabled = isEnabled && !forceDisabled && hasVerified && company.IsActive;

        return new CompanySsoConfigSnapshot(
            CompanyId: company.Id,
            CompanyName: company.Name,
            CompanySlug: company.Slug,
            IsEnabled: isEnabled,
            ForceDisabledByPlatform: forceDisabled,
            PasswordLoginAllowed: cfg?.PasswordLoginAllowed ?? true,
            AutoAcceptInvitationsOnSso: cfg?.AutoAcceptInvitationsOnSso ?? true,
            HasVerifiedDomain: hasVerified,
            IsEffectivelyEnabled: effectivelyEnabled,
            Authority: cfg?.Authority,
            ClientId: cfg?.ClientId,
            MetadataUrl: cfg?.MetadataUrl,
            TokenEndpointAuthMethod: cfg?.TokenEndpointAuthMethod,
            ForceAuthn: cfg?.ForceAuthn ?? false,
            VerifiedDomains: verifiedDomains);
    }
}
