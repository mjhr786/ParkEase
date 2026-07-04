using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Queries.ParkingAvailability;
using ParkingApp.Application.DTOs;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/parking-availability")]
[Produces("application/json")]
public class ParkingAvailabilityController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ParkingAvailabilityController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet("{parkingSpaceId:guid}/forecast")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ParkingAvailabilityForecastDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ParkingAvailabilityForecastDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForecast(
        Guid parkingSpaceId,
        [FromQuery] int horizonHours = 24,
        [FromQuery] int intervalMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.QueryAsync(
            new GetParkingAvailabilityForecastQuery(parkingSpaceId, horizonHours, intervalMinutes),
            cancellationToken);

        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("my-listings")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<ParkingAvailabilityForecastDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyListingsForecasts(
        [FromQuery] int horizonHours = 12,
        [FromQuery] int intervalMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.QueryAsync(
            new GetOwnerParkingAvailabilityForecastsQuery(userId.Value, horizonHours, intervalMinutes),
            cancellationToken);

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
