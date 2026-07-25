using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.Marketplace.Application.Interfaces;

public interface IParkingAvailabilityModelService
{
    Task<ParkingAvailabilityModelPredictionDto?> PredictOccupancyAsync(
        ParkingAvailabilityModelInputDto input,
        int intervalMinutes,
        CancellationToken cancellationToken = default);
}
