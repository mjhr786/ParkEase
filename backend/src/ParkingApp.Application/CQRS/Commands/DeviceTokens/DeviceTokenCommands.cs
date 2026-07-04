using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.DeviceTokens;

// ── Request/Command ────────────────────────────────────────────────────────

public sealed record RegisterDeviceTokenCommand(
    Guid UserId,
    string DeviceId,
    string Platform,
    string FcmToken,
    string? AppVersion
) : ICommand<ApiResponse<bool>>;

// ── Handler ────────────────────────────────────────────────────────────────

public sealed class RegisterDeviceTokenCommandHandler
    : ICommandHandler<RegisterDeviceTokenCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDeviceTokenCommandHandler(IUnitOfWork unitOfWork)
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
