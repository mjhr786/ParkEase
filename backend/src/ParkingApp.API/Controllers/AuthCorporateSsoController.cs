using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.Commands.Auth;
using ParkingApp.Identity.Application.DTOs;

namespace ParkingApp.API.Controllers;

/// <summary>
/// Corporate enterprise SSO (OIDC) — separate from Marketplace <c>/api/auth/external</c>.
/// </summary>
[ApiController]
[Route("api/auth/corporate/sso")]
[AllowAnonymous]
public sealed class AuthCorporateSsoController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AuthCorporateSsoController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>Domain/email → SSO companies (no user existence leak).</summary>
    [HttpGet("discover")]
    [ProducesResponseType(typeof(ApiResponse<CorporateSsoDiscoverResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Discover(
        [FromQuery] string? email,
        [FromQuery] string? domain,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.QueryAsync(
            new CorporateSsoDiscoverQuery(email, domain), cancellationToken);
        return Ok(result);
    }

    /// <summary>SP-initiated OIDC start — returns authorizationUrl for browser redirect.</summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ApiResponse<CorporateSsoStartResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CorporateSsoStartResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CorporateSsoStartResponseDto>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Start(
        [FromBody] CorporateSsoStartDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(new CorporateSsoStartCommand(dto), cancellationToken);
        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "sso_disabled" or "sso_store_unavailable" => StatusCode(StatusCodes.Status503ServiceUnavailable, result),
            "sso_not_available" => NotFound(result),
            _ => BadRequest(result)
        };
    }

    /// <summary>OIDC redirect URI — validates code, provisions membership, redirects with one-time sso_code.</summary>
    [HttpGet("callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(new CorporateSsoCallbackCommand(
            code,
            state,
            error,
            errorDescription,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString()), cancellationToken);

        if (result.Success && !string.IsNullOrWhiteSpace(result.RedirectUrl))
            return Redirect(result.RedirectUrl);

        var payload = new ApiResponse<object>(
            false,
            result.ErrorCode ?? "sso_callback_failed",
            null,
            new List<string> { result.ErrorCode ?? "sso_callback_failed" },
            result.ErrorCode);

        return result.StatusCode switch
        {
            503 => StatusCode(StatusCodes.Status503ServiceUnavailable, payload),
            403 => StatusCode(StatusCodes.Status403Forbidden, payload),
            409 => Conflict(payload),
            502 => StatusCode(StatusCodes.Status502BadGateway, payload),
            _ => BadRequest(payload)
        };
    }

    /// <summary>Exchange one-time sso_code for CorporateLoginResponseDto session.</summary>
    [HttpPost("complete")]
    [ProducesResponseType(typeof(ApiResponse<CorporateLoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CorporateLoginResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CorporateLoginResponseDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Complete(
        [FromBody] CorporateSsoCompleteDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new CorporateSsoCompleteCommand(dto.ExchangeCode ?? dto.SsoCode ?? string.Empty),
            cancellationToken);

        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "sso_disabled" or "sso_store_unavailable" => StatusCode(StatusCodes.Status503ServiceUnavailable, result),
            "account_disabled" or "no_membership" => StatusCode(StatusCodes.Status403Forbidden, result),
            _ => BadRequest(result)
        };
    }
}

public sealed record CorporateSsoCompleteDto(string? ExchangeCode = null, string? SsoCode = null);
