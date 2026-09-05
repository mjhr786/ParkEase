namespace ParkingApp.Corporate.Application.Interfaces;

/// <summary>Lightweight OIDC discovery probe for company admin "Test connection".</summary>
public interface IOidcDiscoveryProbe
{
    Task<OidcDiscoveryProbeResult> ProbeAsync(
        string authority,
        string? metadataUrl,
        CancellationToken cancellationToken = default);
}

public sealed record OidcDiscoveryProbeResult(
    bool Success,
    string ResultCode,
    string? Issuer,
    string? Message);
