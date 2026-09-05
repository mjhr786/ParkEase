using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Corporate.Sso;
using ParkingApp.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;

namespace ParkingApp.API.Controllers;

/// <summary>
/// Company admin Corporate SSO configuration APIs (PR5).
/// Channel matrix: Corporate bound + company match; handler re-checks active Admin membership.
/// </summary>
[ApiController]
[Route("api/v1/corporate/companies/{companyId:guid}/sso")]
[Authorize]
public sealed class CorporateSsoAdminController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public CorporateSsoAdminController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private Guid GetUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoConfigDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetConfig([FromRoute] Guid companyId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.QueryAsync(
            new GetCompanySsoConfigQuery(companyId, GetUserId()), cancellationToken);
        return Map(result);
    }

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoConfigDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(
        [FromRoute] Guid companyId,
        [FromBody] UpsertCompanySsoDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new UpsertCompanySsoCommand(companyId, GetUserId(), dto), cancellationToken);
        return Map(result);
    }

    [HttpPost("domains")]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoDomainDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddDomain(
        [FromRoute] Guid companyId,
        [FromBody] AddCompanySsoDomainDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new AddCompanySsoDomainCommand(companyId, GetUserId(), dto.Domain), cancellationToken);
        return Map(result);
    }

    [HttpPost("domains/{domainId:guid}/verify")]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoDomainDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoDomainDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerifyDomain(
        [FromRoute] Guid companyId,
        [FromRoute] Guid domainId,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new VerifyCompanySsoDomainCommand(companyId, GetUserId(), domainId), cancellationToken);
        return Map(result);
    }

    [HttpDelete("domains/{domainId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveDomain(
        [FromRoute] Guid companyId,
        [FromRoute] Guid domainId,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new RemoveCompanySsoDomainCommand(companyId, GetUserId(), domainId), cancellationToken);
        return Map(result);
    }

    [HttpPost("test")]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoTestResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Test(
        [FromRoute] Guid companyId,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new TestCompanySsoCommand(companyId, GetUserId()), cancellationToken);
        return Map(result);
    }

    [HttpPost("enable")]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Enable(
        [FromRoute] Guid companyId,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new EnableCompanySsoCommand(companyId, GetUserId()), cancellationToken);
        return Map(result);
    }

    [HttpPost("disable")]
    [ProducesResponseType(typeof(ApiResponse<CompanySsoConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Disable(
        [FromRoute] Guid companyId,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new DisableCompanySsoCommand(companyId, GetUserId()), cancellationToken);
        return Map(result);
    }

    [HttpGet("audit")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CorporateSsoAuditEventDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAudit(
        [FromRoute] Guid companyId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new GetCompanySsoAuditQuery(companyId, GetUserId(), take), cancellationToken);
        return Map(result);
    }

    [HttpDelete("links/{linkId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unlink(
        [FromRoute] Guid companyId,
        [FromRoute] Guid linkId,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(
            new UnlinkCompanySsoIdentityCommand(companyId, GetUserId(), linkId), cancellationToken);
        return Map(result);
    }

    private IActionResult Map<T>(ApiResponse<T> result)
    {
        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "company_admin_required" or "membership_inactive" or "company_inactive"
                => StatusCode(StatusCodes.Status403Forbidden, result),
            "company_not_found" or "domain_not_found" or "link_not_found" or "sso_not_available"
                => NotFound(result),
            "domain_claimed"
                => Conflict(result),
            "sso_force_disabled"
                => BadRequest(result),
            _ => BadRequest(result)
        };
    }
}
