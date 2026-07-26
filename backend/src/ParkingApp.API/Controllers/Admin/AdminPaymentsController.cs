using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Commands.Admin;
using ParkingApp.Marketplace.Application.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.API.Controllers.Admin;

[ApiController]
[Route("api/admin/payments")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public sealed class AdminPaymentsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AdminPaymentsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search = null,
        [FromQuery] PaymentStatus? status = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? bookingId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new AdminListPaymentsQuery(search, status, userId, bookingId, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(new AdminGetPaymentQuery(id), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] AdminRefundPaymentRequest body,
        CancellationToken cancellationToken = default)
    {
        var (actorId, actorEmail) = GetActor();
        if (actorId is null)
            return Unauthorized();

        var result = await _dispatcher.SendAsync(
            new AdminProcessRefundCommand(
                actorId.Value,
                actorEmail ?? "unknown",
                id,
                body.Reason,
                body.Amount,
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

    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? GetUserAgent() => Request.Headers.UserAgent.ToString();
}
