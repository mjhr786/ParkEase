using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ParkingApp.API.Options;
using ParkingApp.Application.CQRS;
using ParkingApp.Identity.Application.Commands.Auth;
using ParkingApp.Identity.Application.Commands.Users;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Security;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;


namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IValidator<RegisterDto> _registerValidator;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly IValidator<ExternalLoginDto> _externalLoginValidator;
    private readonly IValidator<LinkExternalLoginDto> _linkExternalLoginValidator;
    private readonly IValidator<SetPasswordDto> _setPasswordValidator;
    private readonly IValidator<ChangePasswordDto> _changePasswordValidator;
    private readonly ChannelIsolationOptions _isolationOptions;

    public AuthController(
        IDispatcher dispatcher,
        IValidator<RegisterDto> registerValidator,
        IValidator<LoginDto> loginValidator,
        IValidator<ExternalLoginDto> externalLoginValidator,
        IValidator<LinkExternalLoginDto> linkExternalLoginValidator,
        IValidator<SetPasswordDto> setPasswordValidator,
        IValidator<ChangePasswordDto> changePasswordValidator,
        IOptions<ChannelIsolationOptions> isolationOptions)
    {
        _dispatcher = dispatcher;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _externalLoginValidator = externalLoginValidator;
        _linkExternalLoginValidator = linkExternalLoginValidator;
        _setPasswordValidator = setPasswordValidator;
        _changePasswordValidator = changePasswordValidator;
        _isolationOptions = isolationOptions.Value;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken cancellationToken)
    {
        var validation = await _registerValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<TokenDto>(false, "Validation failed", null,
                validation.Errors.Select(e => e.ErrorMessage).ToList()));

        var result = await _dispatcher.SendAsync(new RegisterCommand(dto), cancellationToken);
        return result.Success ? Created("", result) : BadRequest(result);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        var validation = await _loginValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new ApiResponse<TokenDto>(false, "Validation failed", null,
                validation.Errors.Select(e => e.ErrorMessage).ToList()));

        var result = await _dispatcher.SendAsync(new LoginCommand(dto), cancellationToken);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Corporate product entry — bootstrap or bound company session (KD-3 / KD-16).</summary>
    [HttpPost("login/corporate")]
    [ProducesResponseType(typeof(ApiResponse<CorporateLoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CorporateLoginResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CorporateLoginResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CorporateLogin([FromBody] CorporateLoginDto dto, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(new CorporateLoginCommand(dto), cancellationToken);
        if (result.Success)
            return Ok(result);

        if (result.Code is "password_login_disabled")
            return StatusCode(StatusCodes.Status403Forbidden, result);

        if (result.Code is "company_selection_required" or "membership_required")
            return BadRequest(result);

        return Unauthorized(result);
    }

    /// <summary>Authenticated channel switch / re-bind (including bootstrap → company).</summary>
    [Authorize]
    [HttpPost("channel")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SwitchChannel([FromBody] SwitchChannelDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new SwitchChannelCommand(userId.Value, dto), cancellationToken);
        if (result.Success)
            return Ok(result);

        if (result.Code is "invalid_channel" or "channel_rebind_forbidden" or "membership_required" or "company_selection_required")
            return BadRequest(result);

        return BadRequest(result);
    }

    /// <summary>Runtime channel context for SPA shells (includes isolationEnabled).</summary>
    [Authorize]
    [HttpGet("channel-context")]
    [ProducesResponseType(typeof(ApiResponse<ChannelContextDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannelContext(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var channelClaim = User.FindFirst(ParkEaseClaimTypes.Channel)?.Value;
        Guid? companyId = null;
        var companyRaw = User.FindFirst(ParkEaseClaimTypes.CompanyId)?.Value;
        if (Guid.TryParse(companyRaw, out var parsedCompany))
            companyId = parsedCompany;
        var companyRole = User.FindFirst(ParkEaseClaimTypes.CompanyRole)?.Value;

        var result = await _dispatcher.QueryAsync(
            new GetChannelContextQuery(userId.Value, channelClaim, companyId, companyRole, _isolationOptions.Enabled),
            cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(new RefreshTokenCommand(dto), cancellationToken);
        if (result.Success)
            return Ok(result);

        // Client input / re-bind policy failures → 400; invalid/expired refresh → 401.
        if (result.Code is "invalid_channel" or "channel_rebind_forbidden")
            return BadRequest(result);

        return Unauthorized(result);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new LogoutCommand(userId.Value), cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var validation = await _changePasswordValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<bool>(
                false,
                "Validation failed",
                false,
                validation.Errors.Select(e => e.ErrorMessage).ToList(),
                "validation_failed"));
        }

        var result = await _dispatcher.SendAsync(new ChangePasswordCommand(userId.Value, dto), cancellationToken);
        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "account_disabled" => new ObjectResult(result) { StatusCode = StatusCodes.Status403Forbidden },
            "password_not_set" => BadRequest(result),
            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// Marketplace social login token-exchange (Google MVP). Always mints Marketplace channel.
    /// Never Corporate / never Admin-via-social.
    /// </summary>
    [HttpPost("external")]
    [ProducesResponseType(typeof(ApiResponse<ExternalAuthSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ExternalAuthSessionDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<ExternalAuthSessionDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<ExternalAuthSessionDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<ExternalAuthSessionDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<ExternalAuthSessionDto>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ExternalLogin([FromBody] ExternalLoginDto dto, CancellationToken cancellationToken)
    {
        var validation = await _externalLoginValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<ExternalAuthSessionDto>(
                false,
                "Validation failed",
                null,
                validation.Errors.Select(e => e.ErrorMessage).ToList(),
                "validation_failed"));
        }

        var result = await _dispatcher.SendAsync(new ExternalLoginCommand(dto), cancellationToken);
        if (result.Success)
            return Ok(result);

        return MapExternalAuthFailure(result);
    }

    /// <summary>Authenticated link of an IdP to the current Marketplace user.</summary>
    [Authorize]
    [HttpPost("external/link")]
    [ProducesResponseType(typeof(ApiResponse<LinkExternalLoginResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<LinkExternalLoginResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<LinkExternalLoginResultDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<LinkExternalLoginResultDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<LinkExternalLoginResultDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkExternalLogin([FromBody] LinkExternalLoginDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var validation = await _linkExternalLoginValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<LinkExternalLoginResultDto>(
                false,
                "Validation failed",
                null,
                validation.Errors.Select(e => e.ErrorMessage).ToList(),
                "validation_failed"));
        }

        var result = await _dispatcher.SendAsync(new LinkExternalLoginCommand(userId.Value, dto), cancellationToken);
        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "provider_already_linked" => new ObjectResult(result) { StatusCode = StatusCodes.Status409Conflict },
            "admin_social_forbidden" or "account_disabled" =>
                new ObjectResult(result) { StatusCode = StatusCodes.Status403Forbidden },
            "invalid_id_token" => new ObjectResult(result) { StatusCode = StatusCodes.Status401Unauthorized },
            "idp_unavailable" => new ObjectResult(result) { StatusCode = StatusCodes.Status503ServiceUnavailable },
            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// Bootstrap password for social-only users (KD-SL-25). Rejects when password already set.
    /// Returns a new Marketplace session (old refresh revoked).
    /// </summary>
    [Authorize]
    [HttpPost("set-password")]
    [ProducesResponseType(typeof(ApiResponse<SetPasswordResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SetPasswordResultDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SetPasswordResultDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var validation = await _setPasswordValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new ApiResponse<SetPasswordResultDto>(
                false,
                "Validation failed",
                null,
                validation.Errors.Select(e => e.ErrorMessage).ToList(),
                "validation_failed"));
        }

        var result = await _dispatcher.SendAsync(new SetPasswordCommand(userId.Value, dto), cancellationToken);
        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "account_disabled" => new ObjectResult(result) { StatusCode = StatusCodes.Status403Forbidden },
            "password_already_set" => BadRequest(result),
            _ => BadRequest(result)
        };
    }

    /// <summary>Enabled external providers for client UI (names only; no secrets).</summary>
    [HttpGet("external/providers")]
    [ProducesResponseType(typeof(ApiResponse<ExternalProvidersDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExternalProviders(CancellationToken cancellationToken)
    {
        var result = await _dispatcher.QueryAsync(new GetExternalProvidersQuery(), cancellationToken);
        return Ok(result);
    }

    private static IActionResult MapExternalAuthFailure(ApiResponse<ExternalAuthSessionDto> result)
    {
        return result.Code switch
        {
            "account_exists" or "provider_already_linked" =>
                new ObjectResult(result) { StatusCode = StatusCodes.Status409Conflict },
            "admin_social_forbidden" or "account_disabled" =>
                new ObjectResult(result) { StatusCode = StatusCodes.Status403Forbidden },
            "invalid_id_token" or "invalid_credentials" =>
                new ObjectResult(result) { StatusCode = StatusCodes.Status401Unauthorized },
            "idp_unavailable" =>
                new ObjectResult(result) { StatusCode = StatusCodes.Status503ServiceUnavailable },
            "rate_limited" =>
                new ObjectResult(result) { StatusCode = StatusCodes.Status429TooManyRequests },
            // provider_disabled, invalid_provider, email_*, nonce_required, validation, etc.
            _ => new BadRequestObjectResult(result)
        };
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public UsersController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.QueryAsync(new GetCurrentUserQuery(userId.Value), cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateUserDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new UpdateUserCommand(userId.Value, dto), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [Authorize]
    [HttpDelete("me")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _dispatcher.SendAsync(new DeleteUserCommand(userId.Value), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

