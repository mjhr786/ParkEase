using Microsoft.Extensions.Logging;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Sso;

internal sealed class GetCompanySsoConfigHandler
    : IQueryHandler<GetCompanySsoConfigQuery, ApiResponse<CompanySsoConfigDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;
    private readonly ISsoSecretCipher _cipher;
    private readonly ICorporateSsoPublicSettings _settings;

    public GetCompanySsoConfigHandler(
        ICorporateUnitOfWork uow,
        ICompanySsoAdminStore store,
        ISsoSecretCipher cipher,
        ICorporateSsoPublicSettings settings)
    {
        _uow = uow;
        _store = store;
        _cipher = cipher;
        _settings = settings;
    }

    public async Task<ApiResponse<CompanySsoConfigDto>> HandleAsync(
        GetCompanySsoConfigQuery query,
        CancellationToken ct = default)
    {
        var gate = await CompanySsoAdminShared.EnsureCompanyAdminAsync<CompanySsoConfigDto>(
            _uow, query.CompanyId, query.AdminUserId, ct);
        if (gate is not null) return gate;

        var cfg = await _store.GetTrackedWithDomainsAsync(query.CompanyId, ct);
        if (cfg is null)
        {
            // No config yet — return empty shell so UI can render setup form.
            return new ApiResponse<CompanySsoConfigDto>(
                true,
                "SSO is not configured yet.",
                new CompanySsoConfigDto(
                    query.CompanyId,
                    "Oidc",
                    false,
                    false,
                    false,
                    null,
                    null,
                    false,
                    false,
                    _settings.OidcRedirectUri,
                    true,
                    true,
                    false,
                    false,
                    null,
                    null,
                    Array.Empty<CompanySsoDomainDto>()));
        }

        return new ApiResponse<CompanySsoConfigDto>(
            true,
            "SSO configuration loaded.",
            CompanySsoAdminShared.ToDto(cfg, _cipher, _settings));
    }
}

internal sealed class UpsertCompanySsoHandler
    : ICommandHandler<UpsertCompanySsoCommand, ApiResponse<CompanySsoConfigDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;
    private readonly ISsoSecretCipher _cipher;
    private readonly ICorporateSsoPublicSettings _settings;
    private readonly ILogger<UpsertCompanySsoHandler> _logger;

    public UpsertCompanySsoHandler(
        ICorporateUnitOfWork uow,
        ICompanySsoAdminStore store,
        ISsoSecretCipher cipher,
        ICorporateSsoPublicSettings settings,
        ILogger<UpsertCompanySsoHandler> logger)
    {
        _uow = uow;
        _store = store;
        _cipher = cipher;
        _settings = settings;
        _logger = logger;
    }

    public async Task<ApiResponse<CompanySsoConfigDto>> HandleAsync(
        UpsertCompanySsoCommand command,
        CancellationToken ct = default)
    {
        var gate = await CompanySsoAdminShared.EnsureCompanyAdminAsync<CompanySsoConfigDto>(
            _uow, command.CompanyId, command.AdminUserId, ct);
        if (gate is not null) return gate;

        try
        {
            _ = CompanySsoAdminShared.ParseProtocol(command.Dto.Protocol);

            var cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct);
            var created = false;
            if (cfg is null)
            {
                cfg = CompanySsoConfiguration.Create(command.CompanyId);
                await _store.AddAsync(cfg, ct);
                created = true;
            }

            // OQ-4: SSO-only (PasswordLoginAllowed=false) requires verified domain + last test ok + confirm.
            // Only enforce when transitioning from allowed → disallowed (already SSO-only may re-save).
            if (!command.Dto.PasswordLoginAllowed && cfg.PasswordLoginAllowed)
            {
                var hasVerifiedDomain = cfg.Domains.Any(d => d.IsVerified && !d.IsDeleted);
                var lastTestOk = string.Equals(cfg.LastTestResult, "ok", StringComparison.OrdinalIgnoreCase);
                if (!hasVerifiedDomain || !lastTestOk || !command.Dto.ConfirmSsoOnly)
                {
                    return CompanySsoAdminShared.Fail<CompanySsoConfigDto>(
                        "SSO-only requires at least one verified domain, a successful connection test, and explicit confirmation.",
                        "sso_only_preconditions_failed");
                }
            }

            string? protectedSecret = null;
            if (command.Dto.ClientSecret is not null)
            {
                // null = leave unchanged; empty string = clear; non-empty = re-protect.
                if (string.IsNullOrWhiteSpace(command.Dto.ClientSecret))
                    protectedSecret = string.Empty;
                else
                    protectedSecret = _cipher.Protect(command.Dto.ClientSecret.Trim());
            }

            cfg.UpsertOidcSettings(
                command.Dto.Authority ?? string.Empty,
                command.Dto.ClientId ?? string.Empty,
                protectedSecret,
                command.Dto.MetadataUrl,
                command.Dto.AttributeMappingJson,
                command.Dto.TokenEndpointAuthMethod,
                command.Dto.PasswordLoginAllowed,
                command.Dto.AutoAcceptInvitationsOnSso,
                command.Dto.ForceAuthn,
                command.Dto.AllowJitProvisioning);

            var action = created ? "sso.config.create" : "sso.config.update";
            await CompanySsoAdminShared.AuditAsync(
                _store,
                action,
                "success",
                command.CompanyId,
                command.AdminUserId,
                ct: ct);

            await _store.SaveChangesAsync(ct);

            _logger.LogInformation(
                "CorporateSso.ConfigChanged CompanyId={CompanyId} ActorUserId={ActorUserId} Action={Action}",
                command.CompanyId,
                command.AdminUserId,
                action);

            // Re-load to ensure domain collection is consistent after create.
            cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct) ?? cfg;

            return new ApiResponse<CompanySsoConfigDto>(
                true,
                created ? "SSO configuration created." : "SSO configuration updated.",
                CompanySsoAdminShared.ToDto(cfg, _cipher, _settings));
        }
        catch (DomainException ex)
        {
            return CompanySsoAdminShared.FromDomainException<CompanySsoConfigDto>(ex);
        }
        catch (ArgumentException ex)
        {
            return CompanySsoAdminShared.Fail<CompanySsoConfigDto>(ex.Message, "validation_failed");
        }
    }
}

internal sealed class AddCompanySsoDomainHandler
    : ICommandHandler<AddCompanySsoDomainCommand, ApiResponse<CompanySsoDomainDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;

    public AddCompanySsoDomainHandler(ICorporateUnitOfWork uow, ICompanySsoAdminStore store)
    {
        _uow = uow;
        _store = store;
    }

    public async Task<ApiResponse<CompanySsoDomainDto>> HandleAsync(
        AddCompanySsoDomainCommand command,
        CancellationToken ct = default)
    {
        var gate = await CompanySsoAdminShared.EnsureCompanyAdminAsync<CompanySsoDomainDto>(
            _uow, command.CompanyId, command.AdminUserId, ct);
        if (gate is not null) return gate;

        try
        {
            var cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct);
            if (cfg is null)
            {
                cfg = CompanySsoConfiguration.Create(command.CompanyId);
                await _store.AddAsync(cfg, ct);
                await _store.SaveChangesAsync(ct);
                cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct)
                    ?? throw new InvalidOperationException("Failed to create SSO configuration");
            }

            var domain = cfg.AddDomain(command.Domain);

            await CompanySsoAdminShared.AuditAsync(
                _store, "sso.domain.add", "success", command.CompanyId, command.AdminUserId,
                detailJson: $"{{\"domain\":\"{domain.Domain}\"}}", ct: ct);

            await _store.SaveChangesAsync(ct);

            return new ApiResponse<CompanySsoDomainDto>(
                true,
                "Domain added. Create the DNS TXT record then verify.",
                new CompanySsoDomainDto(
                    domain.Id,
                    domain.Domain,
                    domain.IsVerified,
                    domain.VerifiedAt,
                    domain.PreferredTxtHost,
                    domain.ExpectedTxtValue));
        }
        catch (DomainException ex)
        {
            return CompanySsoAdminShared.FromDomainException<CompanySsoDomainDto>(ex);
        }
    }
}

internal sealed class VerifyCompanySsoDomainHandler
    : ICommandHandler<VerifyCompanySsoDomainCommand, ApiResponse<CompanySsoDomainDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;
    private readonly IDnsTxtLookup _dns;

    public VerifyCompanySsoDomainHandler(
        ICorporateUnitOfWork uow,
        ICompanySsoAdminStore store,
        IDnsTxtLookup dns)
    {
        _uow = uow;
        _store = store;
        _dns = dns;
    }

    public async Task<ApiResponse<CompanySsoDomainDto>> HandleAsync(
        VerifyCompanySsoDomainCommand command,
        CancellationToken ct = default)
    {
        var gate = await CompanySsoAdminShared.EnsureCompanyAdminAsync<CompanySsoDomainDto>(
            _uow, command.CompanyId, command.AdminUserId, ct);
        if (gate is not null) return gate;

        try
        {
            var cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct);
            if (cfg is null)
                return CompanySsoAdminShared.Fail<CompanySsoDomainDto>("SSO is not configured.", "sso_not_configured");

            var domain = cfg.RequireDomain(command.DomainId);

            if (await _store.IsDomainVerifiedByOtherCompanyAsync(domain.Domain, command.CompanyId, ct))
            {
                await CompanySsoAdminShared.AuditAsync(
                    _store, "sso.domain.verify", "failure", command.CompanyId, command.AdminUserId,
                    "domain_claimed", ct: ct);
                await _store.SaveChangesAsync(ct);
                return CompanySsoAdminShared.Fail<CompanySsoDomainDto>(
                    "This domain is already verified by another company.", "domain_claimed");
            }

            var hosts = new[]
            {
                domain.PreferredTxtHost,
                domain.Domain
            };

            var expected = domain.ExpectedTxtValue;
            var matched = false;
            foreach (var host in hosts)
            {
                var records = await _dns.LookupTxtAsync(host, ct);
                if (records.Any(r => string.Equals(r.Trim().Trim('"'), expected, StringComparison.Ordinal)))
                {
                    matched = true;
                    break;
                }

                // Some resolvers return concatenated multi-string TXT without quotes.
                if (records.Any(r => r.Contains(expected, StringComparison.Ordinal)))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                await CompanySsoAdminShared.AuditAsync(
                    _store, "sso.domain.verify", "failure", command.CompanyId, command.AdminUserId,
                    "domain_unverified",
                    $"{{\"host\":\"{domain.PreferredTxtHost}\"}}",
                    ct);
                await _store.SaveChangesAsync(ct);
                return CompanySsoAdminShared.Fail<CompanySsoDomainDto>(
                    $"DNS TXT record not found. Add TXT on {domain.PreferredTxtHost} (or apex) with value {expected}.",
                    "domain_unverified");
            }

            domain.MarkVerified();

            await CompanySsoAdminShared.AuditAsync(
                _store, "sso.domain.verify", "success", command.CompanyId, command.AdminUserId,
                detailJson: $"{{\"domain\":\"{domain.Domain}\"}}", ct: ct);

            try
            {
                await _store.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                return CompanySsoAdminShared.Fail<CompanySsoDomainDto>(
                    "This domain is already verified by another company.", "domain_claimed");
            }

            return new ApiResponse<CompanySsoDomainDto>(
                true,
                "Domain verified.",
                new CompanySsoDomainDto(
                    domain.Id,
                    domain.Domain,
                    domain.IsVerified,
                    domain.VerifiedAt,
                    domain.PreferredTxtHost,
                    domain.ExpectedTxtValue));
        }
        catch (DomainException ex)
        {
            return CompanySsoAdminShared.FromDomainException<CompanySsoDomainDto>(ex);
        }
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("IX_CompanySsoDomains_Domain_Verified", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class RemoveCompanySsoDomainHandler
    : ICommandHandler<RemoveCompanySsoDomainCommand, ApiResponse<bool>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;

    public RemoveCompanySsoDomainHandler(ICorporateUnitOfWork uow, ICompanySsoAdminStore store)
    {
        _uow = uow;
        _store = store;
    }

    public async Task<ApiResponse<bool>> HandleAsync(
        RemoveCompanySsoDomainCommand command,
        CancellationToken ct = default)
    {
        var gate = await CompanySsoAdminShared.EnsureCompanyAdminAsync<bool>(
            _uow, command.CompanyId, command.AdminUserId, ct);
        if (gate is not null) return gate;

        try
        {
            var cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct);
            if (cfg is null)
                return CompanySsoAdminShared.Fail<bool>("SSO is not configured.", "sso_not_configured");

            cfg.RemoveDomain(command.DomainId);

            await CompanySsoAdminShared.AuditAsync(
                _store, "sso.domain.remove", "success", command.CompanyId, command.AdminUserId,
                detailJson: $"{{\"domainId\":\"{command.DomainId}\"}}", ct: ct);

            await _store.SaveChangesAsync(ct);
            return new ApiResponse<bool>(true, "Domain removed.", true);
        }
        catch (DomainException ex)
        {
            return CompanySsoAdminShared.FromDomainException<bool>(ex);
        }
    }
}

internal sealed class TestCompanySsoHandler
    : ICommandHandler<TestCompanySsoCommand, ApiResponse<CompanySsoTestResultDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;
    private readonly ISsoSecretCipher _cipher;
    private readonly IOidcDiscoveryProbe _probe;

    public TestCompanySsoHandler(
        ICorporateUnitOfWork uow,
        ICompanySsoAdminStore store,
        ISsoSecretCipher cipher,
        IOidcDiscoveryProbe probe)
    {
        _uow = uow;
        _store = store;
        _cipher = cipher;
        _probe = probe;
    }

    public async Task<ApiResponse<CompanySsoTestResultDto>> HandleAsync(
        TestCompanySsoCommand command,
        CancellationToken ct = default)
    {
        var gate = await CompanySsoAdminShared.EnsureCompanyAdminAsync<CompanySsoTestResultDto>(
            _uow, command.CompanyId, command.AdminUserId, ct);
        if (gate is not null) return gate;

        var cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct);
        if (cfg is null)
            return CompanySsoAdminShared.Fail<CompanySsoTestResultDto>("SSO is not configured.", "sso_not_configured");

        if (string.IsNullOrWhiteSpace(cfg.Authority) || string.IsNullOrWhiteSpace(cfg.ClientId))
            return CompanySsoAdminShared.Fail<CompanySsoTestResultDto>(
                "Authority and ClientId are required before testing.", "sso_config_incomplete");

        if (!cfg.HasClientSecret || string.IsNullOrWhiteSpace(cfg.ClientSecretProtected))
            return CompanySsoAdminShared.Fail<CompanySsoTestResultDto>(
                "Client secret is required before testing.", "sso_secret_required");

        if (!_cipher.TryUnprotect(cfg.ClientSecretProtected, out _))
        {
            cfg.RecordTestResult("SecretUnreadable");
            await CompanySsoAdminShared.AuditAsync(
                _store, "sso.test", "failure", command.CompanyId, command.AdminUserId,
                ISsoSecretCipher.SecretUnreadableCode, ct: ct);
            await _store.SaveChangesAsync(ct);
            return CompanySsoAdminShared.Fail<CompanySsoTestResultDto>(
                "Stored client secret cannot be decrypted. Re-enter the client secret.",
                ISsoSecretCipher.SecretUnreadableCode);
        }

        var probe = await _probe.ProbeAsync(cfg.Authority, cfg.MetadataUrl, ct);
        cfg.RecordTestResult(probe.ResultCode);

        await CompanySsoAdminShared.AuditAsync(
            _store,
            "sso.test",
            probe.Success ? "success" : "failure",
            command.CompanyId,
            command.AdminUserId,
            probe.Success ? null : probe.ResultCode,
            ct: ct);

        await _store.SaveChangesAsync(ct);

        var dto = new CompanySsoTestResultDto(probe.Success, probe.ResultCode, probe.Issuer, probe.Message);
        return probe.Success
            ? new ApiResponse<CompanySsoTestResultDto>(true, "OIDC discovery succeeded.", dto)
            : new ApiResponse<CompanySsoTestResultDto>(false, probe.Message ?? "OIDC discovery failed.", dto, null, probe.ResultCode);
    }
}

internal sealed class EnableCompanySsoHandler
    : ICommandHandler<EnableCompanySsoCommand, ApiResponse<CompanySsoConfigDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;
    private readonly ISsoSecretCipher _cipher;
    private readonly ICorporateSsoPublicSettings _settings;

    public EnableCompanySsoHandler(
        ICorporateUnitOfWork uow,
        ICompanySsoAdminStore store,
        ISsoSecretCipher cipher,
        ICorporateSsoPublicSettings settings)
    {
        _uow = uow;
        _store = store;
        _cipher = cipher;
        _settings = settings;
    }

    public async Task<ApiResponse<CompanySsoConfigDto>> HandleAsync(
        EnableCompanySsoCommand command,
        CancellationToken ct = default)
    {
        var gate = await CompanySsoAdminShared.EnsureCompanyAdminAsync<CompanySsoConfigDto>(
            _uow, command.CompanyId, command.AdminUserId, ct);
        if (gate is not null) return gate;

        try
        {
            var cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct);
            if (cfg is null)
                return CompanySsoAdminShared.Fail<CompanySsoConfigDto>("SSO is not configured.", "sso_not_configured");

            if (cfg.ForceDisabledByPlatform)
                return CompanySsoAdminShared.Fail<CompanySsoConfigDto>(
                    "SSO is force-disabled by platform and cannot be enabled.", "sso_force_disabled");

            // Secret must be readable before enable (re-entry path).
            if (cfg.HasClientSecret
                && !string.IsNullOrWhiteSpace(cfg.ClientSecretProtected)
                && !_cipher.TryUnprotect(cfg.ClientSecretProtected, out _))
            {
                return CompanySsoAdminShared.Fail<CompanySsoConfigDto>(
                    "Stored client secret cannot be decrypted. Re-enter the client secret.",
                    ISsoSecretCipher.SecretUnreadableCode);
            }

            cfg.Enable();

            await CompanySsoAdminShared.AuditAsync(
                _store, "sso.enable", "success", command.CompanyId, command.AdminUserId, ct: ct);
            await _store.SaveChangesAsync(ct);

            return new ApiResponse<CompanySsoConfigDto>(
                true,
                "SSO enabled for this company.",
                CompanySsoAdminShared.ToDto(cfg, _cipher, _settings));
        }
        catch (DomainException ex)
        {
            var mapped = CompanySsoAdminShared.FromDomainException<CompanySsoConfigDto>(ex);
            // Normalize domain enable codes to design table names.
            if (mapped.Code is "sso_domain_required")
                return CompanySsoAdminShared.Fail<CompanySsoConfigDto>(
                    "At least one verified domain is required before enable.", "domain_unverified");
            return mapped;
        }
    }
}

internal sealed class DisableCompanySsoHandler
    : ICommandHandler<DisableCompanySsoCommand, ApiResponse<CompanySsoConfigDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;
    private readonly ISsoSecretCipher _cipher;
    private readonly ICorporateSsoPublicSettings _settings;

    public DisableCompanySsoHandler(
        ICorporateUnitOfWork uow,
        ICompanySsoAdminStore store,
        ISsoSecretCipher cipher,
        ICorporateSsoPublicSettings settings)
    {
        _uow = uow;
        _store = store;
        _cipher = cipher;
        _settings = settings;
    }

    public async Task<ApiResponse<CompanySsoConfigDto>> HandleAsync(
        DisableCompanySsoCommand command,
        CancellationToken ct = default)
    {
        var gate = await CompanySsoAdminShared.EnsureCompanyAdminAsync<CompanySsoConfigDto>(
            _uow, command.CompanyId, command.AdminUserId, ct);
        if (gate is not null) return gate;

        var cfg = await _store.GetTrackedWithDomainsAsync(command.CompanyId, ct);
        if (cfg is null)
            return CompanySsoAdminShared.Fail<CompanySsoConfigDto>("SSO is not configured.", "sso_not_configured");

        cfg.Disable();

        await CompanySsoAdminShared.AuditAsync(
            _store, "sso.disable", "success", command.CompanyId, command.AdminUserId, ct: ct);
        await _store.SaveChangesAsync(ct);

        return new ApiResponse<CompanySsoConfigDto>(
            true,
            "SSO disabled for this company.",
            CompanySsoAdminShared.ToDto(cfg, _cipher, _settings));
    }
}

internal sealed class GetCompanySsoAuditHandler
    : IQueryHandler<GetCompanySsoAuditQuery, ApiResponse<IReadOnlyList<CorporateSsoAuditEventDto>>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;

    public GetCompanySsoAuditHandler(ICorporateUnitOfWork uow, ICompanySsoAdminStore store)
    {
        _uow = uow;
        _store = store;
    }

    public async Task<ApiResponse<IReadOnlyList<CorporateSsoAuditEventDto>>> HandleAsync(
        GetCompanySsoAuditQuery query,
        CancellationToken ct = default)
    {
        var gate = await CompanySsoAdminShared.EnsureCompanyAdminAsync<IReadOnlyList<CorporateSsoAuditEventDto>>(
            _uow, query.CompanyId, query.AdminUserId, ct);
        if (gate is not null) return gate;

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

internal sealed class UnlinkCompanySsoIdentityHandler
    : ICommandHandler<UnlinkCompanySsoIdentityCommand, ApiResponse<bool>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICompanySsoAdminStore _store;
    private readonly ICorporateSsoLinkAdmin _links;

    public UnlinkCompanySsoIdentityHandler(
        ICorporateUnitOfWork uow,
        ICompanySsoAdminStore store,
        ICorporateSsoLinkAdmin links)
    {
        _uow = uow;
        _store = store;
        _links = links;
    }

    public async Task<ApiResponse<bool>> HandleAsync(
        UnlinkCompanySsoIdentityCommand command,
        CancellationToken ct = default)
    {
        var gate = await CompanySsoAdminShared.EnsureCompanyAdminAsync<bool>(
            _uow, command.CompanyId, command.AdminUserId, ct);
        if (gate is not null) return gate;

        var ok = await _links.DisableLinkAsync(command.CompanyId, command.LinkId, ct);
        if (!ok)
            return CompanySsoAdminShared.Fail<bool>("SSO identity link was not found.", "link_not_found");

        await CompanySsoAdminShared.AuditAsync(
            _store, "sso.link.unlink", "success", command.CompanyId, command.AdminUserId,
            detailJson: $"{{\"linkId\":\"{command.LinkId}\"}}", ct: ct);
        await _store.SaveChangesAsync(ct);

        return new ApiResponse<bool>(true, "SSO identity link disabled.", true);
    }
}
