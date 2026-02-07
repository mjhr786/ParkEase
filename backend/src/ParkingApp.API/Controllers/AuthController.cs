using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterDto> _registerValidator;
    private readonly IValidator<LoginDto> _loginValidator;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterDto> registerValidator,
        IValidator<LoginDto> loginValidator)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken cancellationToken)
    {
        var validation = await _registerValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<TokenDto>(false, "Validation failed", null, 
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var result = await _authService.RegisterAsync(dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Created("", result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        var validation = await _loginValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<TokenDto>(false, "Validation failed", null,
                validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var result = await _authService.LoginAsync(dto, cancellationToken);
        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(dto, cancellationToken);
        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _authService.LogoutAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _authService.ChangePasswordAsync(userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _userService.GetByIdAsync(userId.Value, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateUserDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _userService.UpdateAsync(userId.Value, dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var result = await _userService.DeleteAsync(userId.Value, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
