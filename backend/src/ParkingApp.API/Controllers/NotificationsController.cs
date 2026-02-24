using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Notifications;
using ParkingApp.Application.CQRS.Queries.Notifications;

namespace ParkingApp.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public NotificationsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = new GetMyNotificationsQuery(userId.Value, page, pageSize);
        var result = await _dispatcher.QueryAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new MarkNotificationAsReadCommand(id, userId.Value);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        
        if (result.Success)
            return Ok();
            
        return BadRequest(result.Message);
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new MarkAllNotificationsAsReadCommand(userId.Value);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        
        if (result.Success)
            return Ok();
            
        return BadRequest(result.Message);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new DeleteNotificationCommand(id, userId.Value);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        
        if (result.Success)
            return Ok();
            
        return BadRequest(result.Message);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
