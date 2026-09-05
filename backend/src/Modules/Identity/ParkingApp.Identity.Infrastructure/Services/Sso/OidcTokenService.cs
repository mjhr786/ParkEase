using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ParkingApp.Identity.Application.Interfaces;

namespace ParkingApp.Identity.Infrastructure.Services.Sso;

/// <summary>OIDC authorization-code + PKCE client and id_token validator for Corporate SSO.</summary>
public sealed class OidcTokenService : IOidcTokenService
{
    private static readonly ConcurrentDictionary<string, (OidcDiscoveryDocument Doc, DateTime ExpiresAt)> DiscoveryCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, (JsonWebKeySet Keys, DateTime ExpiresAt)> JwksCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OidcTokenService> _logger;

    public const string HttpClientName = "CorporateSsoOidc";

    public OidcTokenService(IHttpClientFactory httpClientFactory, ILogger<OidcTokenService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OidcDiscoveryDocument> GetDiscoveryAsync(
        string authority,
        string? metadataUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authority) && string.IsNullOrWhiteSpace(metadataUrl))
            throw new ArgumentException("Authority or metadataUrl is required");

        var url = !string.IsNullOrWhiteSpace(metadataUrl)
            ? metadataUrl.Trim()
            : $"{authority.Trim().TrimEnd('/')}/.well-known/openid-configuration";

        if (DiscoveryCache.TryGetValue(url, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return cached.Doc;

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var json = await JsonSerializer.DeserializeAsync<DiscoveryJson>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Empty OIDC discovery document");

        if (string.IsNullOrWhiteSpace(json.Issuer)
            || string.IsNullOrWhiteSpace(json.AuthorizationEndpoint)
            || string.IsNullOrWhiteSpace(json.TokenEndpoint)
            || string.IsNullOrWhiteSpace(json.JwksUri))
        {
            throw new InvalidOperationException("OIDC discovery document missing required fields");
        }

        var doc = new OidcDiscoveryDocument(
            json.Issuer,
            json.AuthorizationEndpoint,
            json.TokenEndpoint,
            json.JwksUri,
            json.EndSessionEndpoint);

        DiscoveryCache[url] = (doc, DateTime.UtcNow.AddHours(1));
        return doc;
    }

    public string BuildAuthorizationUrl(OidcAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = request.ClientId,
            ["redirect_uri"] = request.RedirectUri,
            ["scope"] = "openid profile email",
            ["state"] = request.State,
            ["nonce"] = request.Nonce,
            ["code_challenge"] = request.CodeChallenge,
            ["code_challenge_method"] = "S256"
        };

        if (!string.IsNullOrWhiteSpace(request.LoginHint))
            query["login_hint"] = request.LoginHint!;
        if (request.ForceAuthn)
            query["prompt"] = "login";

        var qs = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var sep = request.AuthorizationEndpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{request.AuthorizationEndpoint}{sep}{qs}";
    }

    public async Task<OidcTokenExchangeResult> ExchangeCodeAsync(
        OidcTokenExchangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = request.Code,
            ["redirect_uri"] = request.RedirectUri,
            ["code_verifier"] = request.CodeVerifier
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, request.TokenEndpoint);
        var authMethod = string.IsNullOrWhiteSpace(request.TokenEndpointAuthMethod)
            ? "client_secret_post"
            : request.TokenEndpointAuthMethod;

        if (string.Equals(authMethod, "client_secret_basic", StringComparison.OrdinalIgnoreCase))
        {
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{request.ClientId}:{request.ClientSecret}"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
        }
        else
        {
            form["client_id"] = request.ClientId;
            form["client_secret"] = request.ClientSecret;
        }

        message.Content = new FormUrlEncodedContent(form);
        using var response = await client.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OIDC token exchange failed Status={Status} BodyLength={Len}",
                (int)response.StatusCode, body.Length);
            return new OidcTokenExchangeResult(false, null, "token_exchange_failed", "IdP token endpoint rejected the code");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("id_token", out var idTokenEl)
            || idTokenEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(idTokenEl.GetString()))
        {
            return new OidcTokenExchangeResult(false, null, "id_token_missing", "Token response did not include id_token");
        }

        return new OidcTokenExchangeResult(true, idTokenEl.GetString(), null, null);
    }

    public OidcIdTokenValidationResult ValidateIdToken(string idToken, OidcIdTokenValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return Fail("invalid_id_token");

        try
        {
            var keys = GetSigningKeys(context.JwksUri);
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = context.ExpectedIssuer,
                ValidateAudience = true,
                ValidAudience = context.ExpectedAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
                ClockSkew = TimeSpan.FromSeconds(Math.Max(0, context.ClockSkewSeconds)),
                RequireSignedTokens = true,
                RequireExpirationTime = true
            };

            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(idToken, parameters, out var validated);
            if (validated is not JwtSecurityToken jwt)
                return Fail("invalid_id_token");

            if (string.Equals(jwt.Header.Alg, "none", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(jwt.Header.Alg))
            {
                return Fail("invalid_id_token");
            }

            var nonce = principal.FindFirst("nonce")?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
            if (!string.Equals(nonce, context.ExpectedNonce, StringComparison.Ordinal))
                return Fail("nonce_mismatch");

            var sub = principal.FindFirst("sub")?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            if (string.IsNullOrWhiteSpace(sub))
                return Fail("invalid_id_token");

            bool? emailVerified = null;
            var emailVerifiedRaw = principal.FindFirst("email_verified")?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value;
            if (emailVerifiedRaw is not null)
            {
                if (bool.TryParse(emailVerifiedRaw, out var b))
                    emailVerified = b;
                else if (emailVerifiedRaw is "1" or "true" or "True")
                    emailVerified = true;
                else if (emailVerifiedRaw is "0" or "false" or "False")
                    emailVerified = false;
            }

            string? Claim(string type) =>
                principal.FindFirst(type)?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;

            return new OidcIdTokenValidationResult(
                Success: true,
                Subject: sub,
                Email: Claim("email"),
                EmailVerifiedClaim: emailVerified,
                GivenName: Claim("given_name"),
                FamilyName: Claim("family_name"),
                Name: Claim("name"),
                Issuer: jwt.Issuer,
                ErrorCode: null);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "id_token validation failed");
            return Fail("invalid_id_token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected id_token validation error");
            return Fail("invalid_id_token");
        }
    }

    private IEnumerable<SecurityKey> GetSigningKeys(string jwksUri)
    {
        if (JwksCache.TryGetValue(jwksUri, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return cached.Keys.GetSigningKeys();

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = client.GetAsync(jwksUri).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var keySet = new JsonWebKeySet(json);
        JwksCache[jwksUri] = (keySet, DateTime.UtcNow.AddHours(1));
        return keySet.GetSigningKeys();
    }

    private static OidcIdTokenValidationResult Fail(string code) =>
        new(false, null, null, null, null, null, null, null, code);

    /// <summary>Create S256 code_challenge from verifier.</summary>
    public static string CreateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncoder.Encode(hash);
    }

    /// <summary>Cryptographically random opaque string for state/nonce/verifier.</summary>
    public static string CreateRandomToken(int bytes = 32)
    {
        var data = RandomNumberGenerator.GetBytes(bytes);
        return Base64UrlEncoder.Encode(data);
    }

    private sealed class DiscoveryJson
    {
        [JsonPropertyName("issuer")]
        public string Issuer { get; set; } = string.Empty;

        [JsonPropertyName("authorization_endpoint")]
        public string AuthorizationEndpoint { get; set; } = string.Empty;

        [JsonPropertyName("token_endpoint")]
        public string TokenEndpoint { get; set; } = string.Empty;

        [JsonPropertyName("jwks_uri")]
        public string JwksUri { get; set; } = string.Empty;

        [JsonPropertyName("end_session_endpoint")]
        public string? EndSessionEndpoint { get; set; }
    }
}
