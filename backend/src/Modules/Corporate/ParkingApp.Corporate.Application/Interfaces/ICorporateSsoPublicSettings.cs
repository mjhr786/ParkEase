namespace ParkingApp.Corporate.Application.Interfaces;

/// <summary>Host-bound public SSO settings used by company admin config DTOs.</summary>
public interface ICorporateSsoPublicSettings
{
    /// <summary>Canonical OIDC redirect_uri for this environment.</summary>
    string OidcRedirectUri { get; }

    /// <summary>Global <c>CorporateSso:Enabled</c> kill switch.</summary>
    bool GlobalEnabled { get; }

    /// <summary>Staged allow-list: empty means all companies.</summary>
    bool IsCompanyAllowed(Guid companyId);
}
