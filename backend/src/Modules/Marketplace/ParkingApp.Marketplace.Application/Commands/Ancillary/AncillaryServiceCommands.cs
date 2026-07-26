using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.Marketplace.Application.Commands.Ancillary;

public sealed record CreateParkingAncillaryServiceCommand(
    Guid ActorUserId,
    bool IsAdmin,
    CreateParkingAncillaryServiceDto Dto
) : ICommand<ApiResponse<ParkingAncillaryServiceDto>>;

public sealed record UpdateParkingAncillaryServiceCommand(
    Guid ServiceId,
    Guid ActorUserId,
    bool IsAdmin,
    UpdateParkingAncillaryServiceDto Dto
) : ICommand<ApiResponse<ParkingAncillaryServiceDto>>;

public sealed record DeactivateParkingAncillaryServiceCommand(
    Guid ServiceId,
    Guid ActorUserId,
    bool IsAdmin
) : ICommand<ApiResponse<bool>>;
