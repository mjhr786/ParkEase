using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Application.DTOs;


namespace ParkingApp.Marketplace.Application.Interfaces;

public interface IParkingAvailabilityPredictionService
{
    Task<ApiResponse<ParkingAvailabilityForecastDto>> GetForecastAsync(
        Guid parkingSpaceId,
        int horizonHours = 24,
        int intervalMinutes = 60,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<List<ParkingAvailabilityForecastDto>>> GetOwnerForecastsAsync(
        Guid ownerId,
        int horizonHours = 12,
        int intervalMinutes = 60,
        CancellationToken cancellationToken = default);
}

