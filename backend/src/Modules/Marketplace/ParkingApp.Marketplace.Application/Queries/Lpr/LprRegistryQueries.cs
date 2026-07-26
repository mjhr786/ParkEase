using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.Marketplace.Application.Queries.Lpr;

public sealed record GetLprCameraKeysQuery(
    Guid ParkingSpaceId,
    Guid ActorUserId,
    bool IsAdmin
) : IQuery<ApiResponse<IReadOnlyList<LprCameraKeyDto>>>;

public sealed record GetLprPlateRulesQuery(
    Guid ParkingSpaceId,
    Guid ActorUserId,
    bool IsAdmin
) : IQuery<ApiResponse<IReadOnlyList<LprPlateRuleDto>>>;
