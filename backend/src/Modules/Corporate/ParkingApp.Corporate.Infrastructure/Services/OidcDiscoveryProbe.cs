using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ParkingApp.Corporate.Application.Interfaces;

namespace ParkingApp.Corporate.Infrastructure.Services;

/// <summary>HTTP fetch of OIDC discovery document for company admin Test connection.</summary>
public sealed class OidcDiscoveryProbe : IOidcDiscoveryProbe
{
    public const string HttpClientName = "CorporateSsoOidcAdmin";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OidcDiscoveryProbe> _logger;

    public OidcDiscoveryProbe(IHttpClientFactory httpClientFactory, ILogger<OidcDiscoveryProbe> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OidcDiscoveryProbeResult> ProbeAsync(
        string authority,
        string? metadataUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authority) && string.IsNullOrWhiteSpace(metadataUrl))
        {
            return new OidcDiscoveryProbeResult(false, "sso_config_incomplete", null, "Authority or MetadataUrl is required");
        }

        var url = !string.IsNullOrWhiteSpace(metadataUrl)
            ? metadataUrl.Trim()
            : $"{authority.Trim().TrimEnd('/')}/.well-known/openid-configuration";

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new OidcDiscoveryProbeResult(
                    false,
                    "discovery_http_error",
                    null,
                    $"Discovery endpoint returned {(int)response.StatusCode}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var json = await JsonSerializer.DeserializeAsync<DiscoveryJson>(stream, cancellationToken: cancellationToken);
            if (json is null
                || string.IsNullOrWhiteSpace(json.Issuer)
                || string.IsNullOrWhiteSpace(json.AuthorizationEndpoint)
                || string.IsNullOrWhiteSpace(json.TokenEndpoint)
                || string.IsNullOrWhiteSpace(json.JwksUri))
            {
                return new OidcDiscoveryProbeResult(
                    false,
                    "discovery_incomplete",
                    json?.Issuer,
                    "Discovery document missing required OIDC fields");
            }

            return new OidcDiscoveryProbeResult(true, "ok", json.Issuer, "Discovery document is valid");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "OIDC discovery probe failed for {Url}", url);
            return new OidcDiscoveryProbeResult(false, "discovery_failed", null, ex.Message);
        }
    }

    private sealed class DiscoveryJson
    {
        [JsonPropertyName("issuer")]
        public string? Issuer { get; set; }

        [JsonPropertyName("authorization_endpoint")]
        public string? AuthorizationEndpoint { get; set; }

        [JsonPropertyName("token_endpoint")]
        public string? TokenEndpoint { get; set; }

        [JsonPropertyName("jwks_uri")]
        public string? JwksUri { get; set; }
    }
}
