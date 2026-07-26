using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Application.Commands.Lpr;

/// <summary>
/// IoT / simulator command: match plate to booking and check in or out.
/// </summary>
/// <param name="AllowedParkingSpaceIds">
/// When non-null and non-empty, the API key may only access these facilities.
/// Null or empty means unrestricted (dev keys).
/// </param>
/// <param name="SimulatorUserId">
/// When set (simulator path), user must own the facility unless <paramref name="SimulatorIsAdmin"/> is true.
/// </param>
public sealed record ProcessLprAccessCommand(
    string LicensePlate,
    Guid ParkingSpaceId,
    LprDirection Direction,
    DateTime? OccurredAtUtc,
    string Source,
    string? ClientKeyId,
    IReadOnlyList<Guid>? AllowedParkingSpaceIds = null,
    Guid? SimulatorUserId = null,
    bool SimulatorIsAdmin = false,
    double? Confidence = null,
    string? ImageUrl = null,
    string? ImageBase64 = null
) : ICommand<ApiResponse<LprAccessResultDto>>;
