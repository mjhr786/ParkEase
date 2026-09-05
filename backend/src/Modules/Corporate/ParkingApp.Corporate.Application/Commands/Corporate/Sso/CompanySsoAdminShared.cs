using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Enums;
using ParkingApp.Corporate.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Sso;

internal static class CompanySsoAdminShared
{
    public static async Task<ApiResponse<T>?> EnsureCompanyAdminAsync<T>(
        ICorporateUnitOfWork uow,
        Guid companyId,
        Guid adminUserId,
        CancellationToken ct)
    {
        var company = await uow.Companies.GetByIdAsync(companyId, ct);
        if (company is null || company.IsDeleted)
            return Fail<T>("Company not found.", "company_not_found");

        if (!company.IsActive)
            return Fail<T>("This company is inactive.", "company_inactive");

        var membership = await uow.Companies.GetMembershipAsync(companyId, adminUserId, ct);
        if (membership is null || !membership.IsActive)
            return Fail<T>("You are not an active member of this company.", "membership_inactive");

        if (!membership.IsAdmin)
            return Fail<T>("Only company admins can configure SSO.", "company_admin_required");

        return null;
    }

    public static ApiResponse<T> Fail<T>(string message, string code) =>
        new(false, message, default, new List<string> { message }, code);

    public static ApiResponse<T> FromDomainException<T>(DomainException ex)
    {
        var code = ex switch
        {
            BusinessRuleException bre => bre.RuleName,
            ValidationException => "validation_failed",
            ConflictException => "conflict",
            _ => ex.ErrorCode
        };
        return Fail<T>(ex.Message, code);
    }

    public static CompanySsoConfigDto ToDto(
        CompanySsoConfiguration cfg,
        ISsoSecretCipher cipher,
        ICorporateSsoPublicSettings settings)
    {
        var secretUnreadable = false;
        if (cfg.HasClientSecret && !string.IsNullOrWhiteSpace(cfg.ClientSecretProtected))
        {
            if (!cipher.TryUnprotect(cfg.ClientSecretProtected, out _))
                secretUnreadable = true;
        }

        var hasVerified = cfg.Domains.Any(d => d.IsVerified && !d.IsDeleted);
        var effectivelyEnabled = cfg.IsEnabled
            && !cfg.ForceDisabledByPlatform
            && hasVerified
            && settings.GlobalEnabled
            && settings.IsCompanyAllowed(cfg.CompanyId);

        var domains = cfg.Domains
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.Domain)
            .Select(d => new CompanySsoDomainDto(
                d.Id,
                d.Domain,
                d.IsVerified,
                d.VerifiedAt,
                d.PreferredTxtHost,
                d.ExpectedTxtValue))
            .ToList();

        return new CompanySsoConfigDto(
            CompanyId: cfg.CompanyId,
            Protocol: cfg.Protocol.ToString(),
            IsEnabled: cfg.IsEnabled,
            ForceDisabledByPlatform: cfg.ForceDisabledByPlatform,
            IsEffectivelyEnabled: effectivelyEnabled,
            Authority: string.IsNullOrWhiteSpace(cfg.Authority) ? null : cfg.Authority,
            ClientId: string.IsNullOrWhiteSpace(cfg.ClientId) ? null : cfg.ClientId,
            HasClientSecret: cfg.HasClientSecret,
            SecretUnreadable: secretUnreadable,
            RedirectUri: settings.OidcRedirectUri,
            PasswordLoginAllowed: cfg.PasswordLoginAllowed,
            AutoAcceptInvitationsOnSso: cfg.AutoAcceptInvitationsOnSso,
            ForceAuthn: cfg.ForceAuthn,
            AllowJitProvisioning: cfg.AllowJitProvisioning,
            LastTestedAt: cfg.LastTestedAt,
            LastTestResult: cfg.LastTestResult,
            Domains: domains,
            MetadataUrl: string.IsNullOrWhiteSpace(cfg.MetadataUrl) ? null : cfg.MetadataUrl,
            TokenEndpointAuthMethod: string.IsNullOrWhiteSpace(cfg.TokenEndpointAuthMethod) ? null : cfg.TokenEndpointAuthMethod,
            AttributeMappingJson: string.IsNullOrWhiteSpace(cfg.AttributeMappingJson) ? null : cfg.AttributeMappingJson);
    }

    public static async Task AuditAsync(
        ICompanySsoAdminStore store,
        string action,
        string outcome,
        Guid companyId,
        Guid? userId,
        string? errorCode = null,
        string? detailJson = null,
        CancellationToken ct = default)
    {
        // Audit row is the durable trail; Serilog for ConfigChanged is emitted by callers that mutate config.
        var evt = CorporateSsoAuditEvent.Create(
            action,
            outcome,
            companyId,
            userId,
            errorCode,
            detailJson: detailJson);
        await store.WriteAuditAsync(evt, ct);
    }

    public static SsoProtocol ParseProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol))
            return SsoProtocol.Oidc;

        if (Enum.TryParse<SsoProtocol>(protocol.Trim(), ignoreCase: true, out var parsed)
            && parsed == SsoProtocol.Oidc)
        {
            return parsed;
        }

        throw new ValidationException("protocol", "Only Oidc is supported in MVP");
    }
}
