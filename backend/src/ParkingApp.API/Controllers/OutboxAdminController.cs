using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Shared.Outbox;
using ParkingApp.Application.DTOs;

namespace ParkingApp.API.Controllers;

/// <summary>
/// Admin operations for the transactional outbox (failed/pending side-effect messages).
/// </summary>
[ApiController]
[Route("api/admin/outbox")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class OutboxAdminController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public OutboxAdminController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>List outbox messages with optional status/type filter and summary counts.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] OutboxMessageStatusDto? status = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new GetOutboxMessagesQuery(status, type, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Get a single outbox message (includes full payload).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(new GetOutboxMessageByIdQuery(id), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    /// <summary>Requeue one Failed/Pending message for immediate processing.</summary>
    [HttpPost("{id:guid}/requeue")]
    public async Task<IActionResult> Requeue(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.SendAsync(new RequeueOutboxMessageCommand(id), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Requeue all Failed messages.</summary>
    [HttpPost("requeue-failed")]
    public async Task<IActionResult> RequeueAllFailed(CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.SendAsync(new RequeueAllFailedOutboxMessagesCommand(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Process a batch of pending outbox messages now (does not wait for background poll).</summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessNow(
        [FromQuery] int batchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.SendAsync(new ProcessOutboxNowCommand(batchSize), cancellationToken);
        return Ok(result);
    }
}
