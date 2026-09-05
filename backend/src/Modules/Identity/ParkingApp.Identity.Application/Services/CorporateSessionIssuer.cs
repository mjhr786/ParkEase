using Microsoft.Extensions.Logging;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Security;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Identity.Application.Commands.Auth;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Interfaces;

namespace ParkingApp.Identity.Application.Services;

/// <summary>
/// Single Corporate session mint path for password login and SSO complete (KD-CS-25).
/// </summary>
public sealed class CorporateSessionIssuer : ICorporateSessionIssuer
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ICompanyMembershipLookup _memberships;
    private readonly ILogger<CorporateSessionIssuer> _logger;

    public CorporateSessionIssuer(
        IIdentityUnitOfWork unitOfWork,
        ITokenService tokenService,
        ICompanyMembershipLookup memberships,
        ILogger<CorporateSessionIssuer> logger)
    {
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _memberships = memberships;
        _logger = logger;
    }

    public async Task<ApiResponse<CorporateLoginResponseDto>> IssueCorporateSessionAsync(
        User user,
        Guid? preferredCompanyId,
        bool forbidBootstrap,
        CancellationToken cancellationToken = default)
    {
        if (!user.IsActive)
        {
            return new ApiResponse<CorporateLoginResponseDto>(
                false,
                "Account disabled",
                null,
                new List<string> { "Your account has been disabled" },
                "account_disabled");
        }

        var memberships = await _memberships.GetActiveMembershipsAsync(user.Id, cancellationToken);

        if (memberships.Count == 0)
        {
            if (forbidBootstrap)
            {
                return new ApiResponse<CorporateLoginResponseDto>(
                    false,
                    "No company membership",
                    null,
                    new List<string> { "Active membership is required for Corporate SSO" },
                    "no_membership");
            }

            var bootstrapSession = await MintCorporateAsync(user, companyId: null, companyRole: null, cancellationToken);
            _logger.LogInformation("Corporate bootstrap session issued for {UserId}", user.Id);
            return new ApiResponse<CorporateLoginResponseDto>(true, "Corporate bootstrap session",
                new CorporateLoginResponseDto
                {
                    Session = bootstrapSession,
                    IsBootstrap = true,
                    RequiresCompanySelection = false,
                    Memberships = Array.Empty<CompanyMembershipOptionDto>()
                });
        }

        if (preferredCompanyId is Guid requestedCompanyId)
        {
            var match = memberships.FirstOrDefault(m => m.CompanyId == requestedCompanyId);
            if (match is null)
            {
                return new ApiResponse<CorporateLoginResponseDto>(
                    false,
                    "Not a member of the selected company",
                    null,
                    new List<string> { "Active membership required for companyId" },
                    "membership_required");
            }

            var session = await MintCorporateAsync(user, match.CompanyId, match.Role, cancellationToken);
            return new ApiResponse<CorporateLoginResponseDto>(true, "Corporate login successful",
                new CorporateLoginResponseDto
                {
                    Session = session,
                    IsBootstrap = false,
                    RequiresCompanySelection = false,
                    Memberships = Map(memberships)
                });
        }

        if (memberships.Count == 1)
        {
            var only = memberships[0];
            var session = await MintCorporateAsync(user, only.CompanyId, only.Role, cancellationToken);
            return new ApiResponse<CorporateLoginResponseDto>(true, "Corporate login successful",
                new CorporateLoginResponseDto
                {
                    Session = session,
                    IsBootstrap = false,
                    RequiresCompanySelection = false,
                    Memberships = Map(memberships)
                });
        }

        // Multiple memberships, no preferredCompanyId
        if (forbidBootstrap)
        {
            // SSO MVP always passes preferredCompanyId; defensive path.
            return new ApiResponse<CorporateLoginResponseDto>(
                false,
                "Company selection required",
                new CorporateLoginResponseDto
                {
                    Session = null,
                    IsBootstrap = false,
                    RequiresCompanySelection = true,
                    Memberships = Map(memberships)
                },
                new List<string> { "Provide companyId to complete corporate login" },
                "company_selection_required");
        }

        return new ApiResponse<CorporateLoginResponseDto>(
            false,
            "Company selection required",
            new CorporateLoginResponseDto
            {
                Session = null,
                IsBootstrap = false,
                RequiresCompanySelection = true,
                Memberships = Map(memberships)
            },
            new List<string> { "Provide companyId to complete corporate login" },
            "company_selection_required");
    }

    private async Task<TokenDto> MintCorporateAsync(
        User user,
        Guid? companyId,
        string? companyRole,
        CancellationToken cancellationToken)
    {
        var channel = ProductChannel.Corporate;
        var accessToken = _tokenService.GenerateAccessToken(user, channel, companyId, companyRole);
        var refreshToken = _tokenService.GenerateRefreshToken();
        user.RecordLogin(refreshToken, _tokenService.CreateRefreshTokenExpiryUtc());
        user.BindSession(channel, companyId, companyRole);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return AuthTokenDtoFactory.Create(
            accessToken,
            refreshToken,
            user,
            channel,
            companyId,
            companyRole,
            accessTokenExpirationMinutes: _tokenService.AccessTokenExpirationMinutes);
    }

    private static IReadOnlyList<CompanyMembershipOptionDto> Map(IReadOnlyList<CompanyMembershipSummary> memberships) =>
        memberships.Select(m => new CompanyMembershipOptionDto(m.CompanyId, m.CompanyName, m.Role)).ToList();
}
