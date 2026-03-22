using ParkingApp.Application.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ParkingApp.Application.CQRS.Commands.Vehicles;
using ParkingApp.Application.CQRS.Queries.Vehicles;
using ParkingApp.Application.DTOs;

namespace ParkingApp.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public VehiclesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VehicleDto>>> GetMyVehicles()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var result = await _dispatcher.QueryAsync(new GetMyVehiclesQuery(userId.Value));
        return result.Success ? Ok(result.Data) : BadRequest(result.Message);
    }

    [HttpPost]
    public async Task<ActionResult<VehicleDto>> AddVehicle([FromBody] CreateVehicleDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var result = await _dispatcher.SendAsync(new CreateVehicleCommand(userId.Value, dto));
        
        if (!result.Success)
            return BadRequest(result.Message);

        return CreatedAtAction(nameof(GetMyVehicles), new { }, result.Data);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<VehicleDto>> UpdateVehicle(Guid id, [FromBody] UpdateVehicleDto dto)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var result = await _dispatcher.SendAsync(new UpdateVehicleCommand(id, userId.Value, dto));
        
        return result.Success ? Ok(result.Data) : BadRequest(result.Message);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteVehicle(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var result = await _dispatcher.SendAsync(new DeleteVehicleCommand(id, userId.Value));
        
        return result.Success ? NoContent() : BadRequest(result.Message);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
