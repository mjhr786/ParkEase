using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.Marketplace.Application.Commands.EventPackages;

public sealed record CreateEventParkingPackageCommand(
    Guid ActorUserId,
    bool IsAdmin,
    CreateEventParkingPackageDto Dto
) : ICommand<ApiResponse<EventParkingPackageDto>>;

public sealed record UpdateEventParkingPackageCommand(
    Guid PackageId,
    Guid ActorUserId,
    bool IsAdmin,
    UpdateEventParkingPackageDto Dto
) : ICommand<ApiResponse<EventParkingPackageDto>>;

public sealed record DeactivateEventParkingPackageCommand(
    Guid PackageId,
    Guid ActorUserId,
    bool IsAdmin
) : ICommand<ApiResponse<bool>>;

public sealed record PurchaseEventParkingPackageCommand(
    Guid PackageId,
    Guid UserId,
    VehicleType VehicleType,
    string? VehicleNumber,
    string? VehicleModel,
    string? VehicleColor
) : ICommand<ApiResponse<BookingDto>>;
