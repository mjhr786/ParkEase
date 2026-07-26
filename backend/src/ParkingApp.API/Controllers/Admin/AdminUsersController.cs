using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Identity.Application.Commands.Admin;
using ParkingApp.Identity.Application.DTOs;

namespace ParkingApp.API.Controllers.Admin;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AdminUsersController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new AdminListUsersQuery(search, isActive, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(new AdminGetUserQuery(id), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid id,
        [FromBody] AdminSetUserActiveRequest body,
        CancellationToken cancellationToken = default)
    {
        var (actorId, actorEmail) = GetActor();
        if (actorId is null)
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new AdminSetUserActiveCommand(
                actorId.Value,
                actorEmail ?? "unknown",
                id,
                IsActive: true,
                body.Reason,
                GetIp(),
                GetUserAgent()),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        [FromBody] AdminSetUserActiveRequest body,
        CancellationToken cancellationToken = default)
    {
        var (actorId, actorEmail) = GetActor();
        if (actorId is null)
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new AdminSetUserActiveCommand(
                actorId.Value,
                actorEmail ?? "unknown",
                id,
                IsActive: false,
                body.Reason,
                GetIp(),
                GetUserAgent()),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
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
