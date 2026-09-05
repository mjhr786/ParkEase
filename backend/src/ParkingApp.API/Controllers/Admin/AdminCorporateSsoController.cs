using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Corporate.Sso;
using ParkingApp.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;

namespace ParkingApp.API.Controllers.Admin;

/// <summary>
/// Platform admin Corporate SSO oversight (PR6).
/// Channel matrix: <c>/api/admin/**</c> → PlatformAdminRole only.
/// </summary>
[ApiController]
[Route("api/admin/corporate-sso")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public sealed class AdminCorporateSsoController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AdminCorporateSsoController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>List companies with SSO configuration and force-disable status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PlatformCompanySsoPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] bool? forceDisabledOnly = null,
        [FromQuery] bool? enabledOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new ListPlatformCorporateSsoQuery(search, forceDisabledOnly, enabledOnly, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Sticky incident kill switch. Company admin cannot re-enable until clear-force-disable.
    /// Creates an empty SSO config row if none exists so the lock sticks.
    /// </summary>
    [HttpPost("{companyId:guid}/force-disable")]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoConfigDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForceDisable(
        Guid companyId,
        [FromBody] PlatformCorporateSsoActionDto? body = null,
        CancellationToken cancellationToken = default)
    {
        var (actorId, actorEmail) = GetActor();
        if (actorId is null)
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new ForceDisableCompanySsoCommand(
                companyId,
                actorId.Value,
                actorEmail ?? "unknown",
                body?.Reason,
                GetIp(),
                GetUserAgent()),
            cancellationToken);

        return Map(result);
    }

    /// <summary>
    /// Clears platform force-disable only. Does <b>not</b> auto re-enable SSO.
    /// </summary>
    [HttpPost("{companyId:guid}/clear-force-disable")]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoConfigDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearForceDisable(
        Guid companyId,
        [FromBody] PlatformCorporateSsoActionDto? body = null,
        CancellationToken cancellationToken = default)
    {
        var (actorId, actorEmail) = GetActor();
        if (actorId is null)
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new ClearForceDisableCompanySsoCommand(
                companyId,
                actorId.Value,
                actorEmail ?? "unknown",
                body?.Reason,
                GetIp(),
                GetUserAgent()),
            cancellationToken);

        return Map(result);
    }

    /// <summary>Cross-tenant SSO audit view for a company (no company-admin membership required).</summary>
    [HttpGet("{companyId:guid}/audit")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CorporateSsoAuditEventDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CorporateSsoAuditEventDto>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAudit(
        Guid companyId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new GetPlatformCompanySsoAuditQuery(companyId, take),
            cancellationToken);

        if (!result.Success && result.Code == "company_not_found")
            return NotFound(result);

        return Ok(result);
    }

    private IActionResult Map(ApiResponse<CompanySsoConfigDto> result)
    {
        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "company_not_found" or "sso_not_configured" => NotFound(result),
            _ => BadRequest(result)
        };
    }

    private (Guid? Id, string? Email) GetActor()
    {
        var idRaw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.Name);
        Guid? id = Guid.TryParse(idRaw, out var g) ? g : null;
        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email")
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
        return (id, email);
    }

    private string? GetIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() =>
        Request.Headers.UserAgent.ToString();
}
