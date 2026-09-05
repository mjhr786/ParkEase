using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Security;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Identity.Domain.Enums;
using ParkingApp.Identity.Domain.Interfaces;
using TokenDto = ParkingApp.Identity.Application.DTOs.TokenDto;

namespace ParkingApp.Identity.Application.Commands.Auth;

// ═══════════════════════════════════════════════════════════════════════════════
// Commands / queries
// ═══════════════════════════════════════════════════════════════════════════════

public sealed record CorporateLoginCommand(CorporateLoginDto Dto)
    : ICommand<ApiResponse<CorporateLoginResponseDto>>;

public sealed record SwitchChannelCommand(Guid UserId, SwitchChannelDto Dto)
    : ICommand<ApiResponse<TokenDto>>;

/// <param name="IsolationEnabled">From host ChannelIsolation:Enabled (runtime rollback signal).</param>
public sealed record GetChannelContextQuery(
    Guid UserId,
    string? ChannelClaim,
    Guid? CompanyIdClaim,
    string? CompanyRoleClaim,
    bool IsolationEnabled)
    : IQuery<ApiResponse<ChannelContextDto>>;

// ═══════════════════════════════════════════════════════════════════════════════
// Handlers
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed class CorporateLoginHandler : ICommandHandler<CorporateLoginCommand, ApiResponse<CorporateLoginResponseDto>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICorporateSessionIssuer _sessionIssuer;
    private readonly ICompanyMembershipLookup _memberships;
    private readonly ICompanySsoConfigLookup _ssoConfigLookup;
    private readonly ILogger<CorporateLoginHandler> _logger;

    public CorporateLoginHandler(
        IIdentityUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ICorporateSessionIssuer sessionIssuer,
        ICompanyMembershipLookup memberships,
        ICompanySsoConfigLookup ssoConfigLookup,
        ILogger<CorporateLoginHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _sessionIssuer = sessionIssuer;
        _memberships = memberships;
        _ssoConfigLookup = ssoConfigLookup;
        _logger = logger;
    }

    public async Task<ApiResponse<CorporateLoginResponseDto>> HandleAsync(
        CorporateLoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByEmailAsync(command.Dto.Email.ToLower().Trim(), cancellationToken);
        if (user == null || string.IsNullOrEmpty(user.PasswordHash)
            || !_passwordHasher.Verify(command.Dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Corporate login failed for email: {Email}", command.Dto.Email);
            return new ApiResponse<CorporateLoginResponseDto>(
                false, "Invalid credentials", null, new List<string> { "Invalid email or password" });
        }

        if (!user.IsActive)
            return new ApiResponse<CorporateLoginResponseDto>(
                false, "Account disabled", null, new List<string> { "Your account has been disabled" },
                "account_disabled");

        // Password coexistence gate (KD-CS-13) before mint for the company that will bind.
        Guid? companyForGate = command.Dto.CompanyId;
        if (companyForGate is null)
        {
            var memberships = await _memberships.GetActiveMembershipsAsync(user.Id, cancellationToken);
            if (memberships.Count == 1)
                companyForGate = memberships[0].CompanyId;
        }

        if (companyForGate is Guid gateCompanyId)
        {
            var gate = await EvaluatePasswordLoginGateAsync(gateCompanyId, cancellationToken);
            if (gate is not null)
                return gate;
        }

        var result = await _sessionIssuer.IssueCorporateSessionAsync(
            user,
            preferredCompanyId: command.Dto.CompanyId,
            forbidBootstrap: false,
            cancellationToken);

        if (result.Success)
            _logger.LogInformation("Corporate password login: {UserId}", user.Id);

        return result;
    }

    /// <summary>
    /// Block password when company set PasswordLoginAllowed=false and tenant SSO is enabled
    /// (IsEnabled &amp;&amp; !ForceDisabledByPlatform). Default PasswordLoginAllowed=true → no change.
    /// </summary>
    private async Task<ApiResponse<CorporateLoginResponseDto>?> EvaluatePasswordLoginGateAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var snapshot = await _ssoConfigLookup.GetByCompanyIdAsync(companyId, cancellationToken);
        if (snapshot is null)
            return null;

        if (!snapshot.PasswordLoginAllowed
            && snapshot.IsEnabled
            && !snapshot.ForceDisabledByPlatform)
        {
            return new ApiResponse<CorporateLoginResponseDto>(
                false,
                "Password login is disabled for this company. Use company SSO.",
                null,
                new List<string> { "password_login_disabled" },
                "password_login_disabled");
        }

        return null;
    }
}

internal sealed class SwitchChannelHandler : ICommandHandler<SwitchChannelCommand, ApiResponse<TokenDto>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ICompanyMembershipLookup _memberships;
    private readonly ILogger<SwitchChannelHandler> _logger;

    public SwitchChannelHandler(
        IIdentityUnitOfWork unitOfWork,
        ITokenService tokenService,
        ICompanyMembershipLookup memberships,
        ILogger<SwitchChannelHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _memberships = memberships;
        _logger = logger;
    }

    public async Task<ApiResponse<TokenDto>> HandleAsync(
        SwitchChannelCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return new ApiResponse<TokenDto>(false, "User not found", null);

        if (!RefreshTokenHandler.TryParseChannelName(command.Dto.Channel, out var channel))
        {
            return new ApiResponse<TokenDto>(
                false,
                "Invalid channel",
                null,
                new List<string> { "Channel must be Marketplace, Corporate, or Admin" },
                "invalid_channel");
        }

        Guid? companyId = null;
        string? companyRole = null;

        switch (channel)
        {
            case ProductChannel.Marketplace:
                break;

            case ProductChannel.Admin:
                if (user.Role != UserRole.Admin)
                {
                    return new ApiResponse<TokenDto>(
                        false,
                        "Admin channel requires Admin role",
                        null,
                        new List<string> { "Admin channel is not allowed for this user" },
                        "channel_rebind_forbidden");
                }
                break;

            case ProductChannel.Corporate:
            {
                var memberships = await _memberships.GetActiveMembershipsAsync(user.Id, cancellationToken);

                if (command.Dto.Bootstrap || (command.Dto.CompanyId is null && memberships.Count == 0))
                {
                    // Bootstrap: Corporate without company
                    companyId = null;
                    companyRole = null;
                    break;
                }

                if (command.Dto.CompanyId is Guid requested)
                {
                    var match = memberships.FirstOrDefault(m => m.CompanyId == requested);
                    if (match is null)
                    {
                        return new ApiResponse<TokenDto>(
                            false,
                            "Not a member of the selected company",
                            null,
                            new List<string> { "Active membership required for companyId" },
                            "membership_required");
                    }

                    companyId = match.CompanyId;
                    companyRole = match.Role;
                    break;
                }

                if (memberships.Count == 1)
                {
                    companyId = memberships[0].CompanyId;
                    companyRole = memberships[0].Role;
                    break;
                }

                if (memberships.Count == 0)
                {
                    // Treat as bootstrap
                    break;
                }

                return new ApiResponse<TokenDto>(
                    false,
                    "Company selection required",
                    null,
                    new List<string> { "Provide companyId for Corporate channel" },
                    "company_selection_required");
            }

            default:
                return new ApiResponse<TokenDto>(
                    false,
                    "Invalid channel",
                    null,
                    new List<string> { "Channel must be Marketplace, Corporate, or Admin" },
                    "invalid_channel");
        }

        var accessToken = _tokenService.GenerateAccessToken(user, channel, companyId, companyRole);
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RotateRefreshToken(refreshToken, _tokenService.CreateRefreshTokenExpiryUtc());
        user.BindSession(channel, companyId, companyRole);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Channel switch user {UserId} → {Channel} company {CompanyId}",
            user.Id, channel, companyId);

        return new ApiResponse<TokenDto>(true, "Channel switched",
            AuthTokenDtoFactory.Create(accessToken, refreshToken, user, channel, companyId, companyRole,
                accessTokenExpirationMinutes: _tokenService.AccessTokenExpirationMinutes));
    }
}

internal sealed class GetChannelContextHandler : IQueryHandler<GetChannelContextQuery, ApiResponse<ChannelContextDto>>
{
    private readonly ICompanyMembershipLookup _memberships;

    public GetChannelContextHandler(ICompanyMembershipLookup memberships) => _memberships = memberships;

    public async Task<ApiResponse<ChannelContextDto>> HandleAsync(
        GetChannelContextQuery query,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _memberships.GetActiveMembershipsAsync(query.UserId, cancellationToken);
        var channel = string.IsNullOrWhiteSpace(query.ChannelClaim)
            ? ProductChannel.Marketplace.ToString()
            : query.ChannelClaim!;

        var isCorporate = string.Equals(channel, nameof(ProductChannel.Corporate), StringComparison.OrdinalIgnoreCase);
        var isBootstrap = isCorporate && query.CompanyIdClaim is null;

        var dto = new ChannelContextDto
        {
            Channel = channel,
            CompanyId = query.CompanyIdClaim,
            CompanyRole = query.CompanyRoleClaim,
            IsBootstrap = isBootstrap,
            IsolationEnabled = query.IsolationEnabled,
            Memberships = memberships
                .Select(m => new CompanyMembershipOptionDto(m.CompanyId, m.CompanyName, m.Role))
                .ToList()
        };

        return new ApiResponse<ChannelContextDto>(true, null, dto);
    }
}
