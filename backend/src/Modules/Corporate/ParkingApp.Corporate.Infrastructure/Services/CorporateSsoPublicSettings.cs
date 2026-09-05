using Microsoft.Extensions.Configuration;
using ParkingApp.Corporate.Application.Interfaces;

namespace ParkingApp.Corporate.Infrastructure.Services;

/// <summary>
/// Reads CorporateSso host options without coupling Corporate.Application to Identity.Application.
/// Mirrors <c>CorporateSsoOptions</c> shape from configuration section "CorporateSso".
/// </summary>
public sealed class CorporateSsoPublicSettings : ICorporateSsoPublicSettings
{
    private const string CallbackPath = "/api/auth/corporate/sso/callback";

    private readonly IConfiguration _configuration;

    public CorporateSsoPublicSettings(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string OidcRedirectUri
    {
        get
        {
            var baseUrl = (_configuration["CorporateSso:PublicApiBaseUrl"] ?? "https://localhost:5001")
                .Trim()
                .TrimEnd('/');
            return $"{baseUrl}{CallbackPath}";
        }
    }

    public bool GlobalEnabled =>
        string.Equals(_configuration["CorporateSso:Enabled"], "true", StringComparison.OrdinalIgnoreCase)
        || _configuration.GetValue("CorporateSso:Enabled", false);

    public bool IsCompanyAllowed(Guid companyId)
    {
        var section = _configuration.GetSection("CorporateSso:AllowedCompanyIds");
        var ids = section.Get<List<Guid>>() ?? new List<Guid>();
        if (ids.Count == 0)
            return true;
        return ids.Contains(companyId);
    }
}
