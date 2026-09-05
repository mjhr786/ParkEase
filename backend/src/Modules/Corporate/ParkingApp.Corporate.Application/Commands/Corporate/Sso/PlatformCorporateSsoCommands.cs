using ParkingApp.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Sso;

public record ListPlatformCorporateSsoQuery(
    string? Search = null,
    bool? ForceDisabledOnly = null,
    bool? EnabledOnly = null,
    int Page = 1,
    int PageSize = 25
) : IQuery<ApiResponse<PlatformCompanySsoPageDto>>;

public record ForceDisableCompanySsoCommand(
    Guid CompanyId,
    Guid ActorAdminUserId,
    string ActorEmail,
    string? Reason,
    string? IpAddress,
    string? UserAgent
) : ICommand<ApiResponse<CompanySsoConfigDto>>;

public record ClearForceDisableCompanySsoCommand(
    Guid CompanyId,
    Guid ActorAdminUserId,
    string ActorEmail,
    string? Reason,
    string? IpAddress,
    string? UserAgent
) : ICommand<ApiResponse<CompanySsoConfigDto>>;

public record GetPlatformCompanySsoAuditQuery(
    Guid CompanyId,
    int Take = 50
) : IQuery<ApiResponse<IReadOnlyList<CorporateSsoAuditEventDto>>>;
