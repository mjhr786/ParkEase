using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Admin.Application.Queries;
using ParkingApp.Application.CQRS;

namespace ParkingApp.API.Controllers.Admin;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AdminDashboardController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(new GetAdminDashboardQuery(), cancellationToken);
        return Ok(result);
    }
}
