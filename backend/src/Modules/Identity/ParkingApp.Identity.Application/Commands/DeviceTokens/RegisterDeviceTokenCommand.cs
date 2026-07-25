using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;

using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Interfaces;

namespace ParkingApp.Identity.Application.Commands.DeviceTokens;

// G��G�� Request/Command G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

public sealed record RegisterDeviceTokenCommand(
    Guid UserId,
    string DeviceId,
    string Platform,
    string FcmToken,
    string? AppVersion
) : ICommand<ApiResponse<bool>>;

// G��G�� Handler G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

internal sealed class RegisterDeviceTokenCommandHandler
    : ICommandHandler<RegisterDeviceTokenCommand, ApiResponse<bool>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;

    public RegisterDeviceTokenCommandHandler(IIdentityUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> HandleAsync(
        RegisterDeviceTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        // Upsert: one row per (UserId, DeviceId)
        var existing = await _unitOfWork.DeviceTokens
            .GetByDeviceIdAndUserIdAsync(command.DeviceId, command.UserId, cancellationToken);

        if (existing != null)
        {
            // Update the token and last-active timestamp
            existing.FcmToken = command.FcmToken;
            existing.Platform = command.Platform;
            existing.AppVersion = command.AppVersion;
            existing.LastActiveAt = DateTime.UtcNow;
            _unitOfWork.DeviceTokens.Update(existing);
        }
        else
        {
            var deviceToken = new DeviceToken
            {
                UserId = command.UserId,
                DeviceId = command.DeviceId,
                Platform = command.Platform,
                FcmToken = command.FcmToken,
                AppVersion = command.AppVersion,
                LastActiveAt = DateTime.UtcNow
            };
            await _unitOfWork.DeviceTokens.AddAsync(deviceToken, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ApiResponse<bool>(true, "Device token registered successfully", true);
    }
}

