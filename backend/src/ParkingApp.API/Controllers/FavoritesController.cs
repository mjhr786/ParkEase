using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Favorites;
using ParkingApp.Application.CQRS.Queries.Favorites;
using ParkingApp.Application.DTOs;

namespace ParkingApp.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FavoritesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public FavoritesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ParkingSpaceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyFavorites(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetMyFavoritesQuery(userId.Value), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{parkingSpaceId:guid}/toggle")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleFavorite(Guid parkingSpaceId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new ToggleFavoriteCommand(userId.Value, parkingSpaceId), cancellationToken);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
