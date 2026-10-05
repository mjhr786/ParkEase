using Microsoft.AspNetCore.Mvc;
using ParkingApp.API.Filters;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Jobs;
using ParkingApp.Corporate.Application.Commands.Jobs;
using ParkingApp.Marketplace.Application.Commands.Jobs;
using System.Threading;
using System.Threading.Tasks;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/internal/jobs")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public class InternalJobsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public InternalJobsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost("outbox/process")]
    public async Task<IActionResult> ProcessOutbox([FromQuery] int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var processedCount = await _dispatcher.SendAsync(new ProcessOutboxCommand(batchSize), cancellationToken);
        return Ok(new { Processed = processedCount });
    }

    [HttpPost("overstays/detect")]
    public async Task<IActionResult> DetectOverstays([FromQuery] int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.SendAsync(new DetectOverstaysCommand(batchSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("sessions/remind")]
    public async Task<IActionResult> SendSessionReminders([FromQuery] int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.SendAsync(new SendSessionRemindersCommand(batchSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("waitlist/promote")]
    public async Task<IActionResult> PromoteWaitlist([FromQuery] int batchSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.SendAsync(new PromoteWaitlistCommand(batchSize), cancellationToken);
        return Ok(result);
    }
}
