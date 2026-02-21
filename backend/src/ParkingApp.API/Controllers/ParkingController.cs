using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Parking;
using ParkingApp.Application.CQRS.Queries.Parking;
using ParkingApp.Application.DTOs;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ParkingController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IValidator<CreateParkingSpaceDto> _createValidator;

    public ParkingController(IDispatcher dispatcher, IValidator<CreateParkingSpaceDto> createValidator)
    {
        _dispatcher = dispatcher;
        _createValidator = createValidator;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ParkingSpaceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ParkingSpaceDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.QueryAsync(new GetParkingByIdQuery(id), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<ParkingSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] ParkingSearchDto dto, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.QueryAsync(new SearchParkingQuery(dto), cancellationToken);
        return Ok(result);
    }

    [HttpGet("map")]
    [ProducesResponseType(typeof(ApiResponse<List<ParkingMapDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMap([FromQuery] ParkingSearchDto dto, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.QueryAsync(new GetMapCoordinatesQuery(dto), cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpGet("my-listings")]
    [ProducesResponseType(typeof(ApiResponse<List<ParkingSpaceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyListings(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetOwnerParkingsQuery(userId.Value), cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ParkingSpaceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ParkingSpaceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateParkingSpaceDto dto, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<ParkingSpaceDto>(false, "Validation failed", null,
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new CreateParkingCommand(userId.Value, dto), cancellationToken);
        return result.Success
            ? Created($"/api/parking/{result.Data?.Id}", result)
            : BadRequest(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ParkingSpaceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ParkingSpaceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateParkingSpaceDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new UpdateParkingCommand(id, userId.Value, dto), cancellationToken);
        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);

        return Ok(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new DeleteParkingCommand(id, userId.Value), cancellationToken);
        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);

        return Ok(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpPost("{id:guid}/toggle-active")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new ToggleActiveParkingCommand(id, userId.Value), cancellationToken);
        if (!result.Success)
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

