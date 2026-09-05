using ParkingApp.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Sso;

public record GetCompanySsoConfigQuery(
    Guid CompanyId,
    Guid AdminUserId
) : IQuery<ApiResponse<CompanySsoConfigDto>>;

public record UpsertCompanySsoCommand(
    Guid CompanyId,
    Guid AdminUserId,
    UpsertCompanySsoDto Dto
) : ICommand<ApiResponse<CompanySsoConfigDto>>;

public record AddCompanySsoDomainCommand(
    Guid CompanyId,
    Guid AdminUserId,
    string Domain
) : ICommand<ApiResponse<CompanySsoDomainDto>>;

public record VerifyCompanySsoDomainCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid DomainId
) : ICommand<ApiResponse<CompanySsoDomainDto>>;

public record RemoveCompanySsoDomainCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid DomainId
) : ICommand<ApiResponse<bool>>;

public record TestCompanySsoCommand(
    Guid CompanyId,
    Guid AdminUserId
) : ICommand<ApiResponse<CompanySsoTestResultDto>>;

public record EnableCompanySsoCommand(
    Guid CompanyId,
    Guid AdminUserId
) : ICommand<ApiResponse<CompanySsoConfigDto>>;

public record DisableCompanySsoCommand(
    Guid CompanyId,
    Guid AdminUserId
) : ICommand<ApiResponse<CompanySsoConfigDto>>;

public record GetCompanySsoAuditQuery(
    Guid CompanyId,
    Guid AdminUserId,
    int Take = 50
) : IQuery<ApiResponse<IReadOnlyList<CorporateSsoAuditEventDto>>>;

public record UnlinkCompanySsoIdentityCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid LinkId
) : ICommand<ApiResponse<bool>>;
