using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Admin.Application.Queries;
using ParkingApp.Application.CQRS;

namespace ParkingApp.API.Controllers.Admin;

[ApiController]
[Route("api/admin/audit")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public sealed class AdminAuditController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AdminAuditController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? action = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] Guid? actorUserId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new GetAdminAuditLogsQuery(action, resourceType, actorUserId, page, pageSize),
            cancellationToken);
        return Ok(result);
    }
}
