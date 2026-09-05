namespace ParkingApp.Identity.Application.Interfaces;

/// <summary>OIDC discovery, authorization URL, code exchange, and id_token validation (Corporate SSO MVP).</summary>
public interface IOidcTokenService
{
    Task<OidcDiscoveryDocument> GetDiscoveryAsync(string authority, string? metadataUrl, CancellationToken cancellationToken = default);

    string BuildAuthorizationUrl(OidcAuthorizationRequest request);

    Task<OidcTokenExchangeResult> ExchangeCodeAsync(OidcTokenExchangeRequest request, CancellationToken cancellationToken = default);

    OidcIdTokenValidationResult ValidateIdToken(
        string idToken,
        OidcIdTokenValidationContext context);
}

public sealed record OidcDiscoveryDocument(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string JwksUri,
    string? EndSessionEndpoint);

public sealed record OidcAuthorizationRequest(
    string AuthorizationEndpoint,
    string ClientId,
    string RedirectUri,
    string State,
    string Nonce,
    string CodeChallenge,
    string? LoginHint,
    bool ForceAuthn);

public sealed record OidcTokenExchangeRequest(
    string TokenEndpoint,
    string ClientId,
    string ClientSecret,
    string Code,
    string RedirectUri,
    string CodeVerifier,
    string TokenEndpointAuthMethod);

public sealed record OidcTokenExchangeResult(
    bool Success,
    string? IdToken,
    string? ErrorCode,
    string? ErrorDescription);

public sealed record OidcIdTokenValidationContext(
    string ExpectedIssuer,
    string ExpectedAudience,
    string ExpectedNonce,
    string JwksUri,
    int ClockSkewSeconds);

public sealed record OidcIdTokenValidationResult(
    bool Success,
    string? Subject,
    string? Email,
    bool? EmailVerifiedClaim,
    string? GivenName,
    string? FamilyName,
    string? Name,
    string? Issuer,
    string? ErrorCode);
