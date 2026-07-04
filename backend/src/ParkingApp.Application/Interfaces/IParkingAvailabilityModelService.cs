using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.Interfaces;

public interface IParkingAvailabilityModelService
{
    Task<ParkingAvailabilityModelPredictionDto?> PredictOccupancyAsync(
        ParkingAvailabilityModelInputDto input,
        int intervalMinutes,
        CancellationToken cancellationToken = default);
}
