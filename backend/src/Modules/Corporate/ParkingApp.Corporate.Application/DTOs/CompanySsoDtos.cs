namespace ParkingApp.Corporate.Application.DTOs;

/// <summary>Write model for company admin OIDC SSO upsert. ClientSecret is write-only.</summary>
/// <param name="ConfirmSsoOnly">
/// Required when transitioning to PasswordLoginAllowed=false (OQ-4): verified domain + last test success + confirm.
/// </param>
public sealed record UpsertCompanySsoDto(
    string? Protocol,
    string? Authority,
    string? ClientId,
    string? ClientSecret,
    string? MetadataUrl,
    string? AttributeMappingJson,
    string? TokenEndpointAuthMethod,
    bool PasswordLoginAllowed = true,
    bool AutoAcceptInvitationsOnSso = true,
    bool ForceAuthn = false,
    bool AllowJitProvisioning = false,
    bool ConfirmSsoOnly = false);

/// <summary>Read model for company SSO config (secrets redacted).</summary>
public sealed record CompanySsoConfigDto(
    Guid CompanyId,
    string Protocol,
    bool IsEnabled,
    bool ForceDisabledByPlatform,
    bool IsEffectivelyEnabled,
    string? Authority,
    string? ClientId,
    bool HasClientSecret,
    bool SecretUnreadable,
    string RedirectUri,
    bool PasswordLoginAllowed,
    bool AutoAcceptInvitationsOnSso,
    bool ForceAuthn,
    bool AllowJitProvisioning,
    DateTime? LastTestedAt,
    string? LastTestResult,
    IReadOnlyList<CompanySsoDomainDto> Domains,
    string? MetadataUrl = null,
    string? TokenEndpointAuthMethod = null,
    string? AttributeMappingJson = null);

public sealed record CompanySsoDomainDto(
    Guid Id,
    string Domain,
    bool IsVerified,
    DateTime? VerifiedAt,
    string PreferredTxtHost,
    string ExpectedTxtValue);

public sealed record AddCompanySsoDomainDto(string Domain);

public sealed record CompanySsoTestResultDto(
    bool Success,
    string ResultCode,
    string? Issuer,
    string? Message);

public sealed record CorporateSsoAuditEventDto(
    Guid Id,
    string Action,
    string Outcome,
    string? ErrorCode,
    Guid? UserId,
    DateTime CreatedAt,
    string? DetailJson);

// ── Platform admin oversight (PR6) ──────────────────────────────────────────

/// <summary>Summary row for platform SSO oversight list.</summary>
public sealed record PlatformCompanySsoSummaryDto(
    Guid CompanyId,
    string CompanyName,
    string? CompanySlug,
    bool CompanyIsActive,
    bool IsEnabled,
    bool ForceDisabledByPlatform,
    bool IsEffectivelyEnabled,
    DateTime? ForceDisabledAt,
    Guid? ForceDisabledByUserId,
    string Protocol,
    string? Authority,
    DateTime? LastTestedAt,
    string? LastTestResult,
    int VerifiedDomainCount,
    int SsoLoginSuccessCount7d,
    DateTime? UpdatedAt);

public sealed record PlatformCompanySsoPageDto(
    IReadOnlyList<PlatformCompanySsoSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Optional body for platform force-disable / clear (reason audited).</summary>
public sealed record PlatformCorporateSsoActionDto(string? Reason);
