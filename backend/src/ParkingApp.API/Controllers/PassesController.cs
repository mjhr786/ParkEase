using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.ParkingPasses;
using ParkingApp.Application.CQRS.Queries.ParkingPasses;
using ParkingApp.Application.DTOs;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/passes")]
[Authorize]
public class PassesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IValidator<CreateParkingPassDto> _createValidator;
    private readonly IValidator<AssignCorporatePassDto> _corporateValidator;

    public PassesController(
        IDispatcher dispatcher,
        IValidator<CreateParkingPassDto> createValidator,
        IValidator<AssignCorporatePassDto> corporateValidator)
    {
        _dispatcher = dispatcher;
        _createValidator = createValidator;
        _corporateValidator = corporateValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateParkingPassDto dto, CancellationToken cancellationToken)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<ParkingPassDto>(
                false,
                "Validation failed",
                null,
                validation.Errors.Select(error => error.ErrorMessage).ToList()));
        }

        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.SendAsync(new CreateParkingPassCommand(userId.Value, dto), cancellationToken);
        return result.Success
            ? CreatedAtAction(nameof(GetMyActivePasses), new { }, result)
            : BadRequest(result);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyActivePasses(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.QueryAsync(new GetUserActivePassQuery(userId.Value), cancellationToken);
        return Ok(result);
    }

    [HttpPost("corporate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignCorporate([FromBody] AssignCorporatePassDto dto, CancellationToken cancellationToken)
    {
        var validation = await _corporateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<CorporatePassAssignmentResultDto>(
                false,
                "Validation failed",
                null,
                validation.Errors.Select(error => error.ErrorMessage).ToList()));
        }

        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _dispatcher.SendAsync(new AssignCorporatePassCommand(userId.Value, dto), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
