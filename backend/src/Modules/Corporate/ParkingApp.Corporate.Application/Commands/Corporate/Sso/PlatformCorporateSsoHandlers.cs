using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingApp.Admin.Contracts;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Sso;

internal sealed class ListPlatformCorporateSsoHandler
    : IQueryHandler<ListPlatformCorporateSsoQuery, ApiResponse<PlatformCompanySsoPageDto>>
{
    private readonly ICompanySsoAdminStore _store;
    private readonly ICorporateSsoPublicSettings _settings;

    public ListPlatformCorporateSsoHandler(
        ICompanySsoAdminStore store,
        ICorporateSsoPublicSettings settings)
    {
        _store = store;
        _settings = settings;
    }

    public async Task<ApiResponse<PlatformCompanySsoPageDto>> HandleAsync(
        ListPlatformCorporateSsoQuery query,
        CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (rows, total) = await _store.ListPlatformSsoConfigsAsync(
            query.Search,
            query.ForceDisabledOnly,
            query.EnabledOnly,
            page,
            pageSize,
            ct);

        var since = DateTime.UtcNow.AddDays(-7);
        var companyIds = rows.Select(r => r.CompanyId).ToList();
        var loginCounts = await _store.CountSuccessfulSsoLoginsByCompanyAsync(companyIds, since, ct);

        var items = rows.Select(r =>
        {
            var effectivelyEnabled = r.IsEnabled
                && !r.ForceDisabledByPlatform
                && r.VerifiedDomainCount > 0
                && _settings.GlobalEnabled
                && _settings.IsCompanyAllowed(r.CompanyId);

            loginCounts.TryGetValue(r.CompanyId, out var logins7d);

            return new PlatformCompanySsoSummaryDto(
                r.CompanyId,
                r.CompanyName,
                r.CompanySlug,
                r.CompanyIsActive,
                r.IsEnabled,
                r.ForceDisabledByPlatform,
                effectivelyEnabled,
                r.ForceDisabledAt,
                r.ForceDisabledByUserId,
                r.Protocol,
                r.Authority,
                r.LastTestedAt,
                r.LastTestResult,
                r.VerifiedDomainCount,
                logins7d,
                r.UpdatedAt);
        }).ToList();

        return new ApiResponse<PlatformCompanySsoPageDto>(
            true,
            "Corporate SSO configurations loaded.",
            new PlatformCompanySsoPageDto(items, page, pageSize, total));
    }
}

internal sealed class ForceDisableCompanySsoHandler
    : ICommandHandler<ForceDisableCompanySsoCommand, ApiResponse<CompanySsoConfigDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;
    private readonly ISsoSecretCipher _cipher;
    private readonly ICorporateSsoPublicSettings _settings;
    private readonly IAdminAudit _adminAudit;
    private readonly ILogger<ForceDisableCompanySsoHandler> _logger;

    public ForceDisableCompanySsoHandler(
        ICorporateUnitOfWork uow,
        ICompanySsoAdminStore store,
        ISsoSecretCipher cipher,
        ICorporateSsoPublicSettings settings,
        IAdminAudit adminAudit,
        ILogger<ForceDisableCompanySsoHandler> logger)
    {
        _uow = uow;
        _store = store;
        _cipher = cipher;
        _settings = settings;
        _adminAudit = adminAudit;
        _logger = logger;
    }

    public async Task<ApiResponse<CompanySsoConfigDto>> HandleAsync(
        ForceDisableCompanySsoCommand command,
        CancellationToken ct = default)
    {
        if (command.ActorAdminUserId == Guid.Empty)
            return CompanySsoAdminShared.Fail<CompanySsoConfigDto>("Actor is required.", "actor_required");

        var company = await _uow.Companies.GetByIdAsync(command.CompanyId, ct);
        if (company is null || company.IsDeleted)
            return CompanySsoAdminShared.Fail<CompanySsoConfigDto>("Company not found.", "company_not_found");

        try
        {
            var cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct);
            var created = false;
            if (cfg is null)
            {
                // Incident posture: create a config row so the sticky lock blocks future enable.
                cfg = CompanySsoConfiguration.Create(command.CompanyId);
                await _store.AddAsync(cfg, ct);
                created = true;
            }

            var alreadyForced = cfg.ForceDisabledByPlatform;
            cfg.ForceDisableByPlatform(command.ActorAdminUserId);

            var reason = TruncateReason(command.Reason);
            await CompanySsoAdminShared.AuditAsync(
                _store,
                "sso.force_disable",
                "success",
                command.CompanyId,
                command.ActorAdminUserId,
                detailJson: JsonSerializer.Serialize(new
                {
                    reason,
                    alreadyForced,
                    createdConfig = created
                }),
                ct: ct);

            _adminAudit.Stage(new AdminAuditEntry(
                command.ActorAdminUserId,
                command.ActorEmail ?? "unknown",
                "CorporateSso.ForceDisable",
                "CompanySsoConfiguration",
                command.CompanyId,
                JsonSerializer.Serialize(new
                {
                    companyId = command.CompanyId,
                    companyName = company.Name,
                    reason,
                    alreadyForced,
                    createdConfig = created
                }),
                command.IpAddress,
                command.UserAgent));

            await _store.SaveChangesAsync(ct);

            _logger.LogWarning(
                "CorporateSso.ForceDisable CompanyId={CompanyId} ActorUserId={ActorUserId} AlreadyForced={AlreadyForced}",
                command.CompanyId,
                command.ActorAdminUserId,
                alreadyForced);

            // Reload for domain collection after create.
            if (created)
                cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct) ?? cfg;

            return new ApiResponse<CompanySsoConfigDto>(
                true,
                alreadyForced
                    ? "SSO was already force-disabled; lock refreshed."
                    : "SSO force-disabled by platform. Company admin cannot re-enable until cleared.",
                CompanySsoAdminShared.ToDto(cfg, _cipher, _settings));
        }
        catch (DomainException ex)
        {
            return CompanySsoAdminShared.FromDomainException<CompanySsoConfigDto>(ex);
        }
    }

    private static string? TruncateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var t = reason.Trim();
        return t.Length <= 500 ? t : t[..500];
    }
}

internal sealed class ClearForceDisableCompanySsoHandler
    : ICommandHandler<ClearForceDisableCompanySsoCommand, ApiResponse<CompanySsoConfigDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;
    private readonly ISsoSecretCipher _cipher;
    private readonly ICorporateSsoPublicSettings _settings;
    private readonly IAdminAudit _adminAudit;
    private readonly ILogger<ClearForceDisableCompanySsoHandler> _logger;

    public ClearForceDisableCompanySsoHandler(
        ICorporateUnitOfWork uow,
        ICompanySsoAdminStore store,
        ISsoSecretCipher cipher,
        ICorporateSsoPublicSettings settings,
        IAdminAudit adminAudit,
        ILogger<ClearForceDisableCompanySsoHandler> logger)
    {
        _uow = uow;
        _store = store;
        _cipher = cipher;
        _settings = settings;
        _adminAudit = adminAudit;
        _logger = logger;
    }

    public async Task<ApiResponse<CompanySsoConfigDto>> HandleAsync(
        ClearForceDisableCompanySsoCommand command,
        CancellationToken ct = default)
    {
        if (command.ActorAdminUserId == Guid.Empty)
            return CompanySsoAdminShared.Fail<CompanySsoConfigDto>("Actor is required.", "actor_required");

        var company = await _uow.Companies.GetByIdAsync(command.CompanyId, ct);
        if (company is null || company.IsDeleted)
            return CompanySsoAdminShared.Fail<CompanySsoConfigDto>("Company not found.", "company_not_found");

        var cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct);
        if (cfg is null)
            return CompanySsoAdminShared.Fail<CompanySsoConfigDto>("SSO is not configured.", "sso_not_configured");

        var wasForced = cfg.ForceDisabledByPlatform;
        cfg.ClearForceDisableByPlatform();
        // Does NOT auto-enable — company admin must call enable again (KD-CS-24).

        var reason = TruncateReason(command.Reason);
        await CompanySsoAdminShared.AuditAsync(
            _store,
            "sso.clear_force_disable",
            "success",
            command.CompanyId,
            command.ActorAdminUserId,
            detailJson: JsonSerializer.Serialize(new { reason, wasForced }),
            ct: ct);

        _adminAudit.Stage(new AdminAuditEntry(
            command.ActorAdminUserId,
            command.ActorEmail ?? "unknown",
            "CorporateSso.ClearForceDisable",
            "CompanySsoConfiguration",
            command.CompanyId,
            JsonSerializer.Serialize(new
            {
                companyId = command.CompanyId,
                companyName = company.Name,
                reason,
                wasForced,
                remainsDisabled = !cfg.IsEnabled
            }),
            command.IpAddress,
            command.UserAgent));

        await _store.SaveChangesAsync(ct);

        _logger.LogWarning(
            "CorporateSso.ClearForceDisable CompanyId={CompanyId} ActorUserId={ActorUserId} WasForced={WasForced} RemainsDisabled={RemainsDisabled}",
            command.CompanyId,
            command.ActorAdminUserId,
            wasForced,
            !cfg.IsEnabled);

        return new ApiResponse<CompanySsoConfigDto>(
            true,
            wasForced
                ? "Platform force-disable cleared. SSO remains disabled until company admin enables it."
                : "No platform force-disable was set; configuration unchanged aside from audit.",
            CompanySsoAdminShared.ToDto(cfg, _cipher, _settings));
    }

    private static string? TruncateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var t = reason.Trim();
        return t.Length <= 500 ? t : t[..500];
    }
}

internal sealed class GetPlatformCompanySsoAuditHandler
    : IQueryHandler<GetPlatformCompanySsoAuditQuery, ApiResponse<IReadOnlyList<CorporateSsoAuditEventDto>>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;

    public GetPlatformCompanySsoAuditHandler(ICorporateUnitOfWork uow, ICompanySsoAdminStore store)
    {
        _uow = uow;
        _store = store;
    }

    public async Task<ApiResponse<IReadOnlyList<CorporateSsoAuditEventDto>>> HandleAsync(
        GetPlatformCompanySsoAuditQuery query,
        CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetByIdAsync(query.CompanyId, ct);
        if (company is null || company.IsDeleted)
            return CompanySsoAdminShared.Fail<IReadOnlyList<CorporateSsoAuditEventDto>>(
                "Company not found.", "company_not_found");

        var take = Math.Clamp(query.Take, 1, 200);
        var events = await _store.GetRecentAuditAsync(query.CompanyId, take, ct);
        var dtos = events.Select(e => new CorporateSsoAuditEventDto(
            e.Id,
            e.Action,
            e.Outcome,
            e.ErrorCode,
            e.UserId,
            e.CreatedAt,
            e.DetailJson)).ToList();

        return new ApiResponse<IReadOnlyList<CorporateSsoAuditEventDto>>(
            true, "SSO audit events loaded.", dtos);
    }
}
