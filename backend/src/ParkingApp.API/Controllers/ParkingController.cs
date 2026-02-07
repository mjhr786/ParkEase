using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParkingController : ControllerBase
{
    private readonly IParkingSpaceService _parkingService;
    private readonly IValidator<CreateParkingSpaceDto> _createValidator;

    public ParkingController(
        IParkingSpaceService parkingService,
        IValidator<CreateParkingSpaceDto> createValidator)
    {
        _parkingService = parkingService;
        _createValidator = createValidator;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _parkingService.GetByIdAsync(id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] ParkingSearchDto dto, CancellationToken cancellationToken)
    {
        var result = await _parkingService.SearchAsync(dto, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpGet("my-listings")]
    public async Task<IActionResult> GetMyListings(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _parkingService.GetByOwnerAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateParkingSpaceDto dto, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<ParkingSpaceDto>(false, "Validation failed", null,
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _parkingService.CreateAsync(userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Created($"/api/parking/{result.Data?.Id}", result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateParkingSpaceDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _parkingService.UpdateAsync(id, userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _parkingService.DeleteAsync(id, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
    }

    [Authorize(Roles = "Vendor,Admin")]
    [HttpPost("{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _parkingService.ToggleActiveAsync(id, userId.Value, cancellationToken);
        if (!result.Success)
        {
            return result.Message == "Unauthorized" ? Forbid() : BadRequest(result);
        }

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
