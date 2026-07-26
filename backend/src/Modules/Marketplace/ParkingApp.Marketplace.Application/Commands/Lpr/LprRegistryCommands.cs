using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Application.Commands.Lpr;

public sealed record CreateLprCameraKeyCommand(
    Guid ParkingSpaceId,
    Guid ActorUserId,
    bool IsAdmin,
    string Name,
    string? KeyId
) : ICommand<ApiResponse<LprCameraKeyCreatedDto>>;

public sealed record SetLprCameraKeyEnabledCommand(
    Guid ParkingSpaceId,
    Guid CameraKeyId,
    Guid ActorUserId,
    bool IsAdmin,
    bool IsEnabled
) : ICommand<ApiResponse<LprCameraKeyDto>>;

public sealed record DeleteLprCameraKeyCommand(
    Guid ParkingSpaceId,
    Guid CameraKeyId,
    Guid ActorUserId,
    bool IsAdmin
) : ICommand<ApiResponse<bool>>;

public sealed record CreateLprPlateRuleCommand(
    Guid ParkingSpaceId,
    Guid ActorUserId,
    bool IsAdmin,
    string LicensePlate,
    LprPlateRuleType RuleType,
    string? Note
) : ICommand<ApiResponse<LprPlateRuleDto>>;

public sealed record SetLprPlateRuleEnabledCommand(
    Guid ParkingSpaceId,
    Guid RuleId,
    Guid ActorUserId,
    bool IsAdmin,
    bool IsEnabled
) : ICommand<ApiResponse<LprPlateRuleDto>>;

public sealed record DeleteLprPlateRuleCommand(
    Guid ParkingSpaceId,
    Guid RuleId,
    Guid ActorUserId,
    bool IsAdmin
) : ICommand<ApiResponse<bool>>;
