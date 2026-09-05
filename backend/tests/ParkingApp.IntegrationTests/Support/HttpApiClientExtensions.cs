using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;

namespace ParkingApp.IntegrationTests.Support;

/// <summary>Shared JSON + auth helpers for L4 HTTP integration tests.</summary>
public static class HttpApiClientExtensions
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void UseBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public static void ClearBearer(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
    }

    public static async Task<ApiResponse<T>?> ReadApiResponseAsync<T>(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions);
    }

    public static async Task<(HttpResponseMessage Response, ApiResponse<TokenDto>? Body)> RegisterAsync(
        this HttpClient client,
        string email,
        string password = "TestPass1!",
        string firstName = "Test",
        string lastName = "User",
        string phone = "+919876543210",
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterDto(email, password, firstName, lastName, phone),
            JsonOptions,
            cancellationToken);
        var body = await response.ReadApiResponseAsync<TokenDto>(cancellationToken);
        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, ApiResponse<TokenDto>? Body)> LoginAsync(
        this HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginDto(email, password),
            JsonOptions,
            cancellationToken);
        var body = await response.ReadApiResponseAsync<TokenDto>(cancellationToken);
        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, ApiResponse<TokenDto>? Body)> RefreshAsync(
        this HttpClient client,
        string refreshToken,
        string? channel = null,
        Guid? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenDto(refreshToken, channel, companyId),
            JsonOptions,
            cancellationToken);
        var body = await response.ReadApiResponseAsync<TokenDto>(cancellationToken);
        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, ApiResponse<TokenDto>? Body)> SwitchChannelAsync(
        this HttpClient client,
        string channel,
        Guid? companyId = null,
        bool bootstrap = false,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/channel",
            new SwitchChannelDto(channel, companyId, bootstrap),
            JsonOptions,
            cancellationToken);
        var body = await response.ReadApiResponseAsync<TokenDto>(cancellationToken);
        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, ApiResponse<CorporateLoginResponseDto>? Body)> CorporateLoginAsync(
        this HttpClient client,
        string email,
        string password,
        Guid? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login/corporate",
            new CorporateLoginDto(email, password, companyId),
            JsonOptions,
            cancellationToken);
        var body = await response.ReadApiResponseAsync<CorporateLoginResponseDto>(cancellationToken);
        return (response, body);
    }

    /// <summary>Register a unique user and return minted Marketplace tokens.</summary>
    public static async Task<TokenDto> RegisterAndGetTokensAsync(
        this HttpClient client,
        string? emailPrefix = null,
        CancellationToken cancellationToken = default)
    {
        var email = $"{emailPrefix ?? "user"}_{Guid.NewGuid():N}@it.parkease.test";
        var (response, body) = await client.RegisterAsync(email, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        if (body?.Success != true || body.Data is null)
            throw new InvalidOperationException(
                $"Register failed: {response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");
        return body.Data;
    }

    /// <summary>Register and return both email and tokens (for multi-user journeys).</summary>
    public static async Task<(string Email, TokenDto Tokens)> RegisterUserAsync(
        this HttpClient client,
        string? emailPrefix = null,
        string password = "TestPass1!",
        CancellationToken cancellationToken = default)
    {
        var email = $"{emailPrefix ?? "user"}_{Guid.NewGuid():N}@it.parkease.test";
        var (response, body) = await client.RegisterAsync(email, password, cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        if (body?.Success != true || body.Data is null)
            throw new InvalidOperationException(
                $"Register failed: {response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");
        return (email, body.Data);
    }

    public static async Task<(HttpResponseMessage Response, ApiResponse<ExternalAuthSessionDto>? Body)> ExternalLoginAsync(
        this HttpClient client,
        string provider,
        string idToken,
        string? nonce = null,
        string? linkPassword = null,
        string? firstName = null,
        string? lastName = null,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/external",
            new ExternalLoginDto(provider, idToken, nonce, firstName, lastName, linkPassword),
            JsonOptions,
            cancellationToken);
        var body = await response.ReadApiResponseAsync<ExternalAuthSessionDto>(cancellationToken);
        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, ApiResponse<ExternalProvidersDto>? Body)> GetExternalProvidersAsync(
        this HttpClient client,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync("/api/auth/external/providers", cancellationToken);
        var body = await response.ReadApiResponseAsync<ExternalProvidersDto>(cancellationToken);
        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, ApiResponse<LinkExternalLoginResultDto>? Body)> LinkExternalAsync(
        this HttpClient client,
        string provider,
        string idToken,
        string? nonce = null,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/external/link",
            new LinkExternalLoginDto(provider, idToken, nonce),
            JsonOptions,
            cancellationToken);
        var body = await response.ReadApiResponseAsync<LinkExternalLoginResultDto>(cancellationToken);
        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, ApiResponse<SetPasswordResultDto>? Body)> SetPasswordAsync(
        this HttpClient client,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/set-password",
            new SetPasswordDto(newPassword),
            JsonOptions,
            cancellationToken);
        var body = await response.ReadApiResponseAsync<SetPasswordResultDto>(cancellationToken);
        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, ApiResponse<bool?>? Body)> ChangePasswordAsync(
        this HttpClient client,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordDto(currentPassword, newPassword),
            JsonOptions,
            cancellationToken);
        var body = await response.ReadApiResponseAsync<bool?>(cancellationToken);
        return (response, body);
    }
}
