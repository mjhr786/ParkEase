using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Identity.Application.Options;
using ParkingApp.Identity.Application.Services;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Enums;
using ParkingApp.Identity.Domain.Interfaces;

namespace ParkingApp.Identity.Application.Commands.Auth;

// ── Commands / queries / DTOs ─────────────────────────────────────────────────

public sealed record CorporateSsoDiscoverQuery(string? Email, string? Domain)
    : IQuery<ApiResponse<CorporateSsoDiscoverResponseDto>>;

public sealed record CorporateSsoStartCommand(CorporateSsoStartDto Dto)
    : ICommand<ApiResponse<CorporateSsoStartResponseDto>>;

public sealed record CorporateSsoCallbackCommand(
    string? Code,
    string? State,
    string? Error,
    string? ErrorDescription,
    string? IpAddress,
    string? UserAgent)
    : ICommand<CorporateSsoCallbackResult>;

public sealed record CorporateSsoCompleteCommand(string ExchangeCode)
    : ICommand<ApiResponse<CorporateLoginResponseDto>>;

public sealed record CorporateSsoDiscoverResponseDto(
    bool SsoAvailable,
    IReadOnlyList<CorporateSsoCompanyOptionDto> Companies);

public sealed record CorporateSsoCompanyOptionDto(Guid CompanyId, string Name, string? Slug);

public sealed record CorporateSsoStartDto(
    string? CompanySlug,
    Guid? CompanyId,
    string? Email,
    string? EmailDomain,
    string? EmailHint,
    string? ReturnUrl,
    string Client = "web");

public sealed record CorporateSsoStartResponseDto(string AuthorizationUrl, string State);

public sealed record CorporateSsoCallbackResult(
    bool Success,
    string? RedirectUrl,
    string? ErrorCode,
    int StatusCode);

// ── Discover ──────────────────────────────────────────────────────────────────

internal sealed class CorporateSsoDiscoverHandler
    : IQueryHandler<CorporateSsoDiscoverQuery, ApiResponse<CorporateSsoDiscoverResponseDto>>
{
    private readonly ICompanySsoConfigLookup _lookup;
    private readonly IOptionsMonitor<CorporateSsoOptions> _options;

    public CorporateSsoDiscoverHandler(
        ICompanySsoConfigLookup lookup,
        IOptionsMonitor<CorporateSsoOptions> options)
    {
        _lookup = lookup;
        _options = options;
    }

    public async Task<ApiResponse<CorporateSsoDiscoverResponseDto>> HandleAsync(
        CorporateSsoDiscoverQuery query,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            return OkDiscover(false, Array.Empty<CorporateSsoCompanyOptionDto>());
        }

        var domain = ResolveDomain(query.Email, query.Domain);
        if (domain is null)
            return OkDiscover(false, Array.Empty<CorporateSsoCompanyOptionDto>());

        var snapshot = await _lookup.GetByVerifiedDomainAsync(domain, cancellationToken);
        if (snapshot is null || !IsEffectivelyEnabled(snapshot, opts))
            return OkDiscover(false, Array.Empty<CorporateSsoCompanyOptionDto>());

        return OkDiscover(true, new[]
        {
            new CorporateSsoCompanyOptionDto(snapshot.CompanyId, snapshot.CompanyName, snapshot.CompanySlug)
        });
    }

    private static ApiResponse<CorporateSsoDiscoverResponseDto> OkDiscover(
        bool available,
        IReadOnlyList<CorporateSsoCompanyOptionDto> companies) =>
        new(true, null, new CorporateSsoDiscoverResponseDto(available, companies));

    internal static string? ResolveDomain(string? email, string? domain)
    {
        if (!string.IsNullOrWhiteSpace(domain))
            return domain.Trim().ToLowerInvariant().TrimStart('@');

        if (string.IsNullOrWhiteSpace(email))
            return null;

        var at = email.IndexOf('@');
        if (at <= 0 || at >= email.Length - 1)
            return null;

        return email[(at + 1)..].Trim().ToLowerInvariant();
    }

    internal static bool IsEffectivelyEnabled(CompanySsoConfigSnapshot snapshot, CorporateSsoOptions opts) =>
        opts.Enabled
        && snapshot.IsEnabled
        && !snapshot.ForceDisabledByPlatform
        && snapshot.HasVerifiedDomain
        && opts.IsCompanyAllowed(snapshot.CompanyId);
}

// ── Start ─────────────────────────────────────────────────────────────────────

internal sealed class CorporateSsoStartHandler
    : ICommandHandler<CorporateSsoStartCommand, ApiResponse<CorporateSsoStartResponseDto>>
{
    private readonly ICompanySsoConfigLookup _lookup;
    private readonly ISsoLoginStateStore _stateStore;
    private readonly IOidcTokenService _oidc;
    private readonly IOptionsMonitor<CorporateSsoOptions> _options;
    private readonly ICorporateSsoMetrics _metrics;
    private readonly ILogger<CorporateSsoStartHandler> _logger;

    public CorporateSsoStartHandler(
        ICompanySsoConfigLookup lookup,
        ISsoLoginStateStore stateStore,
        IOidcTokenService oidc,
        IOptionsMonitor<CorporateSsoOptions> options,
        ICorporateSsoMetrics metrics,
        ILogger<CorporateSsoStartHandler> logger)
    {
        _lookup = lookup;
        _stateStore = stateStore;
        _oidc = oidc;
        _options = options;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<ApiResponse<CorporateSsoStartResponseDto>> HandleAsync(
        CorporateSsoStartCommand command,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            _metrics.RecordStart("sso_disabled");
            return new ApiResponse<CorporateSsoStartResponseDto>(
                false, "Corporate SSO is disabled", null,
                new List<string> { "sso_disabled" }, "sso_disabled");
        }

        var dto = command.Dto;
        var clientType = string.Equals(dto.Client, "mobile", StringComparison.OrdinalIgnoreCase) ? "mobile" : "web";

        CompanySsoConfigSnapshot? snapshot = null;
        if (dto.CompanyId is Guid companyId)
            snapshot = await _lookup.GetByCompanyIdAsync(companyId, cancellationToken);
        else if (!string.IsNullOrWhiteSpace(dto.CompanySlug))
            snapshot = await _lookup.GetBySlugAsync(dto.CompanySlug!, cancellationToken);
        else
        {
            var domain = CorporateSsoDiscoverHandler.ResolveDomain(dto.Email, dto.EmailDomain);
            if (domain is not null)
                snapshot = await _lookup.GetByVerifiedDomainAsync(domain, cancellationToken);
        }

        if (snapshot is null || !CorporateSsoDiscoverHandler.IsEffectivelyEnabled(snapshot, opts))
        {
            _metrics.RecordStart("sso_not_available");
            return new ApiResponse<CorporateSsoStartResponseDto>(
                false, "SSO is not available for this company", null,
                new List<string> { "sso_not_available" }, "sso_not_available");
        }

        if (string.IsNullOrWhiteSpace(snapshot.Authority) || string.IsNullOrWhiteSpace(snapshot.ClientId))
        {
            _metrics.RecordStart("sso_config_incomplete");
            return new ApiResponse<CorporateSsoStartResponseDto>(
                false, "SSO configuration incomplete", null,
                new List<string> { "sso_config_incomplete" }, "sso_config_incomplete");
        }

        OidcDiscoveryDocument discovery;
        try
        {
            discovery = await _oidc.GetDiscoveryAsync(snapshot.Authority!, snapshot.MetadataUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OIDC discovery failed for company {CompanyId}", snapshot.CompanyId);
            _metrics.RecordStart("idp_discovery_failed");
            return new ApiResponse<CorporateSsoStartResponseDto>(
                false, "Identity provider discovery failed", null,
                new List<string> { "idp_discovery_failed" }, "idp_discovery_failed");
        }

        var stateId = SsoCrypto.CreateRandomToken(32);
        var nonce = SsoCrypto.CreateRandomToken(24);
        var codeVerifier = SsoCrypto.CreateRandomToken(48);
        var codeChallenge = SsoCrypto.CreateCodeChallengeS256(codeVerifier);
        var returnUrl = NormalizeReturnUrl(dto.ReturnUrl, clientType, opts);

        var now = DateTime.UtcNow;
        var state = new SsoLoginStateRecord(
            StateId: stateId,
            CompanyId: snapshot.CompanyId,
            Nonce: nonce,
            CodeVerifier: codeVerifier,
            ReturnUrl: returnUrl,
            ClientType: clientType,
            EmailHint: string.IsNullOrWhiteSpace(dto.EmailHint) ? dto.Email : dto.EmailHint,
            CreatedAtUtc: now,
            ExpiresAtUtc: now.AddSeconds(opts.StateTtlSeconds));

        try
        {
            await _stateStore.StoreAsync(state, TimeSpan.FromSeconds(opts.StateTtlSeconds), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "CorporateSso.StateStoreUnavailable Operation={Operation} ExceptionType={ExceptionType}",
                "state.store",
                ex.GetType().Name);
            _metrics.RecordStart("sso_store_unavailable");
            return new ApiResponse<CorporateSsoStartResponseDto>(
                false, "SSO temporarily unavailable", null,
                new List<string> { "sso_store_unavailable" }, "sso_store_unavailable");
        }

        var authUrl = _oidc.BuildAuthorizationUrl(new OidcAuthorizationRequest(
            discovery.AuthorizationEndpoint,
            snapshot.ClientId!,
            opts.GetOidcRedirectUri(),
            stateId,
            nonce,
            codeChallenge,
            state.EmailHint,
            snapshot.ForceAuthn));

        _logger.LogInformation(
            "CorporateSso.Start CompanyId={CompanyId} Client={Client} Outcome={Outcome}",
            snapshot.CompanyId,
            clientType,
            "ok");
        _metrics.RecordStart("ok");

        return new ApiResponse<CorporateSsoStartResponseDto>(
            true, null, new CorporateSsoStartResponseDto(authUrl, stateId));
    }

    private static string NormalizeReturnUrl(string? returnUrl, string clientType, CorporateSsoOptions opts)
    {
        if (clientType == "web")
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
                return "/corporate/dashboard";
            var path = returnUrl.Trim();
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return "/corporate/dashboard";
            if (!path.StartsWith("/corporate", StringComparison.OrdinalIgnoreCase))
                return "/corporate/dashboard";
            return path;
        }

        var candidate = string.IsNullOrWhiteSpace(returnUrl)
            ? opts.GetCanonicalMobileCompleteUri()
            : returnUrl.Trim();

        foreach (var allowed in opts.MobileRedirectUris)
        {
            if (!string.IsNullOrWhiteSpace(allowed)
                && candidate.StartsWith(allowed.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return opts.GetCanonicalMobileCompleteUri();
    }
}

// ── Callback ──────────────────────────────────────────────────────────────────

internal sealed class CorporateSsoCallbackHandler
    : ICommandHandler<CorporateSsoCallbackCommand, CorporateSsoCallbackResult>
{
    private static readonly Regex EmailShaped = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly ICompanySsoConfigLookup _lookup;
    private readonly ISsoLoginStateStore _stateStore;
    private readonly ISsoExchangeCodeStore _exchangeStore;
    private readonly IOidcTokenService _oidc;
    private readonly ISsoClientSecretAccessor _secretAccessor; // Corporate.Contracts
    private readonly IIdentityUnitOfWork _uow;
    private readonly ICompanyMembershipLookup _memberships;
    private readonly ICorporateSsoMembershipProvisioner _provisioner;
    private readonly IOptionsMonitor<CorporateSsoOptions> _options;
    private readonly ICorporateSsoMetrics _metrics;
    private readonly ILogger<CorporateSsoCallbackHandler> _logger;

    public CorporateSsoCallbackHandler(
        ICompanySsoConfigLookup lookup,
        ISsoLoginStateStore stateStore,
        ISsoExchangeCodeStore exchangeStore,
        IOidcTokenService oidc,
        ParkingApp.Corporate.Contracts.ISsoClientSecretAccessor secretAccessor,
        IIdentityUnitOfWork uow,
        ICompanyMembershipLookup memberships,
        ICorporateSsoMembershipProvisioner provisioner,
        IOptionsMonitor<CorporateSsoOptions> options,
        ICorporateSsoMetrics metrics,
        ILogger<CorporateSsoCallbackHandler> logger)
    {
        _lookup = lookup;
        _stateStore = stateStore;
        _exchangeStore = exchangeStore;
        _oidc = oidc;
        _secretAccessor = secretAccessor;
        _uow = uow;
        _memberships = memberships;
        _provisioner = provisioner;
        _options = options;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<CorporateSsoCallbackResult> HandleAsync(
        CorporateSsoCallbackCommand command,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
            return Fail("sso_disabled", 503);

        if (!string.IsNullOrWhiteSpace(command.Error))
            return Fail("idp_error", 400);

        if (string.IsNullOrWhiteSpace(command.State) || string.IsNullOrWhiteSpace(command.Code))
            return Fail("invalid_state", 400);

        SsoLoginStateRecord? state;
        try
        {
            state = await _stateStore.TryConsumeAsync(command.State!, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "CorporateSso.StateStoreUnavailable Operation={Operation} ExceptionType={ExceptionType}",
                "state.consume",
                ex.GetType().Name);
            return Fail("sso_store_unavailable", 503);
        }

        if (state is null)
            return Fail("invalid_state", 400);

        var snapshot = await _lookup.GetByCompanyIdAsync(state.CompanyId, cancellationToken);
        if (snapshot is null || !CorporateSsoDiscoverHandler.IsEffectivelyEnabled(snapshot, opts))
            return Fail("sso_not_available", 403, state.CompanyId);

        if (string.IsNullOrWhiteSpace(snapshot.Authority) || string.IsNullOrWhiteSpace(snapshot.ClientId))
            return Fail("sso_config_incomplete", 500, state.CompanyId);

        var (secretOk, clientSecret) = await _secretAccessor.TryGetClientSecretAsync(state.CompanyId, cancellationToken);
        if (!secretOk || string.IsNullOrEmpty(clientSecret))
            return Fail("SecretUnreadable", 503, state.CompanyId);

        OidcDiscoveryDocument discovery;
        try
        {
            discovery = await _oidc.GetDiscoveryAsync(snapshot.Authority!, snapshot.MetadataUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discovery failed during callback CompanyId={CompanyId}", state.CompanyId);
            return Fail("idp_discovery_failed", 502, state.CompanyId);
        }

        var authMethod = string.IsNullOrWhiteSpace(snapshot.TokenEndpointAuthMethod)
            ? opts.DefaultTokenEndpointAuthMethod
            : snapshot.TokenEndpointAuthMethod!;

        var exchange = await _oidc.ExchangeCodeAsync(new OidcTokenExchangeRequest(
            discovery.TokenEndpoint,
            snapshot.ClientId!,
            clientSecret!,
            command.Code!,
            opts.GetOidcRedirectUri(),
            state.CodeVerifier,
            authMethod), cancellationToken);

        if (!exchange.Success || string.IsNullOrWhiteSpace(exchange.IdToken))
        {
            _metrics.RecordTokenExchangeError(snapshot.Authority);
            return Fail(exchange.ErrorCode ?? "token_exchange_failed", 400, state.CompanyId);
        }

        var validated = _oidc.ValidateIdToken(exchange.IdToken!, new OidcIdTokenValidationContext(
            discovery.Issuer,
            snapshot.ClientId!,
            state.Nonce,
            discovery.JwksUri,
            opts.ClockSkewSeconds));

        if (!validated.Success)
            return Fail(validated.ErrorCode ?? "invalid_id_token", 400, state.CompanyId);

        var email = ResolveEmail(validated, snapshot);
        if (email is null)
            return Fail("email_required", 400, state.CompanyId);

        if (validated.EmailVerifiedClaim is false)
            return Fail("email_unverified", 400, state.CompanyId);

        try
        {
            var userResult = await ResolveUserAndMembershipAsync(
                state.CompanyId,
                validated.Subject!,
                validated.Issuer ?? discovery.Issuer,
                email,
                emailVerified: validated.EmailVerifiedClaim is not false,
                validated.GivenName,
                validated.FamilyName,
                cancellationToken);

            if (!userResult.Success)
                return Fail(userResult.ErrorCode!, userResult.StatusCode, state.CompanyId);

            var exchangeCode = SsoCrypto.CreateRandomToken(32);
            var now = DateTime.UtcNow;
            await _exchangeStore.StoreAsync(new SsoExchangeCodeRecord(
                exchangeCode,
                userResult.UserId!.Value,
                state.CompanyId,
                ForbidBootstrap: true,
                now,
                now.AddSeconds(opts.ExchangeCodeTtlSeconds)),
                TimeSpan.FromSeconds(opts.ExchangeCodeTtlSeconds),
                cancellationToken);

            var redirect = BuildCompleteRedirect(state, exchangeCode, opts);
            _logger.LogInformation(
                "CorporateSso.CallbackSuccess CompanyId={CompanyId} UserId={UserId}",
                state.CompanyId,
                userResult.UserId);
            _metrics.RecordCallback("ok");
            return new CorporateSsoCallbackResult(true, redirect, null, 302);
        }
        catch (InvalidOperationException ex) when (ex.Message is "sso_subject_conflict" or "sso_identity_mismatch" or "sso_link_disabled")
        {
            return Fail(ex.Message, 409, state.CompanyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSO callback user resolution failed CompanyId={CompanyId}", state.CompanyId);
            return Fail("sso_callback_failed", 500, state.CompanyId);
        }
    }

    private static string? ResolveEmail(OidcIdTokenValidationResult validated, CompanySsoConfigSnapshot snapshot)
    {
        string? resolved = null;
        if (!string.IsNullOrWhiteSpace(validated.Email) && EmailShaped.IsMatch(validated.Email.Trim()))
            resolved = validated.Email.Trim().ToLowerInvariant();

        if (resolved is null)
            return null;

        var at = resolved.IndexOf('@');
        if (at <= 0) return null;
        var domain = resolved[(at + 1)..];
        var verified = snapshot.VerifiedDomains ?? Array.Empty<string>();
        if (!verified.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase)))
            return null;

        return resolved;
    }

    private async Task<(bool Success, Guid? UserId, string? ErrorCode, int StatusCode)> ResolveUserAndMembershipAsync(
        Guid companyId,
        string subject,
        string issuer,
        string email,
        bool emailVerified,
        string? firstName,
        string? lastName,
        CancellationToken cancellationToken)
    {
        var link = await _uow.CorporateSsoLinks.GetByCompanySubjectAsync(
            companyId, SsoProtocol.Oidc, subject, cancellationToken);

        User? user = null;
        if (link is not null)
        {
            if (link.IsDisabled)
                return (false, null, "sso_link_disabled", 403);

            user = await _uow.Users.GetByIdAsync(link.UserId, cancellationToken);
            if (user is null)
                return (false, null, "account_disabled", 403);

            if (!user.IsActive)
                return (false, null, "account_disabled", 403);

            // KD-CS-26: IdP email drift updates ProviderEmail only
            link.RecordUse(email);
            _uow.CorporateSsoLinks.Update(link);
            await _uow.SaveChangesAsync(cancellationToken);
        }
        else
        {
            user = await _uow.Users.GetByEmailAsync(email, cancellationToken);
            if (user is not null && !user.IsActive)
                return (false, null, "account_disabled", 403);

            // Ensure email user does not already have a different subject link for this company
            if (user is not null)
            {
                var existingForUser = await _uow.CorporateSsoLinks.GetByCompanyUserAsync(
                    companyId, user.Id, cancellationToken);
                if (existingForUser is not null
                    && !string.Equals(existingForUser.Subject, subject, StringComparison.Ordinal))
                {
                    return (false, null, "sso_identity_mismatch", 409);
                }
            }
        }

        if (user is not null)
        {
            var membership = await _memberships.GetActiveMembershipAsync(user.Id, companyId, cancellationToken);
            if (membership is null)
            {
                var provision = await _provisioner.TryAcceptPendingInviteAsync(
                    companyId, user.Id, email, cancellationToken);
                if (!provision.Accepted)
                    return (false, null, "no_membership", 403);
            }

            if (link is null)
                await CreateLinkAsync(user.Id, companyId, subject, issuer, email, cancellationToken);

            return (true, user.Id, null, 200);
        }

        // No user and no subject link: only create when invite can grant membership (KD-CS-6)
        var newUser = User.RegisterFromCorporateSso(email, firstName, lastName, emailVerified);
        await _uow.Users.AddAsync(newUser, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var inviteResult = await _provisioner.TryAcceptPendingInviteAsync(
            companyId, newUser.Id, email, cancellationToken);
        if (!inviteResult.Accepted)
        {
            newUser.Deactivate();
            _uow.Users.Update(newUser);
            await _uow.SaveChangesAsync(cancellationToken);
            return (false, null, "no_membership", 403);
        }

        await CreateLinkAsync(newUser.Id, companyId, subject, issuer, email, cancellationToken);
        return (true, newUser.Id, null, 200);
    }

    private async Task CreateLinkAsync(
        Guid userId,
        Guid companyId,
        string subject,
        string issuer,
        string email,
        CancellationToken cancellationToken)
    {
        // Subject uniqueness race → surface as conflict
        var subjectTaken = await _uow.CorporateSsoLinks.GetByCompanySubjectAsync(
            companyId, SsoProtocol.Oidc, subject, cancellationToken);
        if (subjectTaken is not null && subjectTaken.UserId != userId)
            throw new InvalidOperationException("sso_subject_conflict");

        if (subjectTaken is not null)
        {
            subjectTaken.RecordUse(email);
            _uow.CorporateSsoLinks.Update(subjectTaken);
            await _uow.SaveChangesAsync(cancellationToken);
            return;
        }

        var created = CorporateSsoIdentityLink.Create(
            companyId, userId, SsoProtocol.Oidc, subject, issuer, email);
        await _uow.CorporateSsoLinks.AddAsync(created, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private static string BuildCompleteRedirect(
        SsoLoginStateRecord state,
        string exchangeCode,
        CorporateSsoOptions opts)
    {
        var codeQ = Uri.EscapeDataString(exchangeCode);
        if (string.Equals(state.ClientType, "mobile", StringComparison.OrdinalIgnoreCase))
        {
            var baseUri = state.ReturnUrl;
            if (string.IsNullOrWhiteSpace(baseUri)
                || !opts.MobileRedirectUris.Any(a =>
                    !string.IsNullOrWhiteSpace(a)
                    && baseUri.StartsWith(a.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                baseUri = opts.GetCanonicalMobileCompleteUri();
            }

            var sep = baseUri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{baseUri}{sep}sso_code={codeQ}";
        }

        var frontend = (opts.FrontendBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        return $"{frontend}/corporate/sso/complete?sso_code={codeQ}";
    }

    private CorporateSsoCallbackResult Fail(string code, int status, Guid? companyId = null)
    {
        _logger.LogWarning(
            "CorporateSso.CallbackFail CompanyId={CompanyId} ErrorCode={ErrorCode} StatusCode={StatusCode}",
            companyId,
            code,
            status);
        _metrics.RecordCallback("fail", code);
        return new CorporateSsoCallbackResult(false, null, code, status);
    }
}

// ── Complete ──────────────────────────────────────────────────────────────────

internal sealed class CorporateSsoCompleteHandler
    : ICommandHandler<CorporateSsoCompleteCommand, ApiResponse<CorporateLoginResponseDto>>
{
    private readonly ISsoExchangeCodeStore _exchangeStore;
    private readonly IIdentityUnitOfWork _uow;
    private readonly ICorporateSessionIssuer _sessionIssuer;
    private readonly IOptionsMonitor<CorporateSsoOptions> _options;
    private readonly ICorporateSsoMetrics _metrics;
    private readonly ILogger<CorporateSsoCompleteHandler> _logger;

    public CorporateSsoCompleteHandler(
        ISsoExchangeCodeStore exchangeStore,
        IIdentityUnitOfWork uow,
        ICorporateSessionIssuer sessionIssuer,
        IOptionsMonitor<CorporateSsoOptions> options,
        ICorporateSsoMetrics metrics,
        ILogger<CorporateSsoCompleteHandler> logger)
    {
        _exchangeStore = exchangeStore;
        _uow = uow;
        _sessionIssuer = sessionIssuer;
        _options = options;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<ApiResponse<CorporateLoginResponseDto>> HandleAsync(
        CorporateSsoCompleteCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _metrics.RecordComplete("sso_disabled", "sso_disabled");
            return new ApiResponse<CorporateLoginResponseDto>(
                false, "Corporate SSO is disabled", null,
                new List<string> { "sso_disabled" }, "sso_disabled");
        }

        if (string.IsNullOrWhiteSpace(command.ExchangeCode))
        {
            _metrics.RecordComplete("invalid_exchange_code", "invalid_exchange_code");
            return new ApiResponse<CorporateLoginResponseDto>(
                false, "Invalid exchange code", null,
                new List<string> { "invalid_exchange_code" }, "invalid_exchange_code");
        }

        SsoExchangeCodeRecord? record;
        try
        {
            record = await _exchangeStore.TryConsumeAsync(command.ExchangeCode.Trim(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "CorporateSso.StateStoreUnavailable Operation={Operation} ExceptionType={ExceptionType}",
                "exchange.consume",
                ex.GetType().Name);
            _metrics.RecordComplete("sso_store_unavailable", "sso_store_unavailable");
            return new ApiResponse<CorporateLoginResponseDto>(
                false, "SSO temporarily unavailable", null,
                new List<string> { "sso_store_unavailable" }, "sso_store_unavailable");
        }

        if (record is null)
        {
            _metrics.RecordComplete("invalid_exchange_code", "invalid_exchange_code");
            return new ApiResponse<CorporateLoginResponseDto>(
                false, "Invalid or expired exchange code", null,
                new List<string> { "invalid_exchange_code" }, "invalid_exchange_code");
        }

        var user = await _uow.Users.GetByIdAsync(record.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            _metrics.RecordComplete("account_disabled", "account_disabled");
            return new ApiResponse<CorporateLoginResponseDto>(
                false, "Account disabled", null,
                new List<string> { "account_disabled" }, "account_disabled");
        }

        var session = await _sessionIssuer.IssueCorporateSessionAsync(
            user,
            preferredCompanyId: record.CompanyId,
            forbidBootstrap: true,
            cancellationToken);

        if (session.Success)
        {
            _logger.LogInformation(
                "CorporateSso.Complete CompanyId={CompanyId} UserId={UserId} Outcome={Outcome}",
                record.CompanyId,
                record.UserId,
                "ok");
            _metrics.RecordComplete("ok");
        }
        else
        {
            _logger.LogWarning(
                "CorporateSso.Complete CompanyId={CompanyId} UserId={UserId} Outcome={Outcome} ErrorCode={ErrorCode}",
                record.CompanyId,
                record.UserId,
                "fail",
                session.Code);
            _metrics.RecordComplete("fail", session.Code);
        }

        return session;
    }
}
