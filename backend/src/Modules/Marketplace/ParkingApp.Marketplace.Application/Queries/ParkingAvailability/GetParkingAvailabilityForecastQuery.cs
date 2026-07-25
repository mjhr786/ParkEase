using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Application.Interfaces;

using ParkingApp.Application.Interfaces;


namespace ParkingApp.Marketplace.Application.Queries.ParkingAvailability;

public sealed record GetParkingAvailabilityForecastQuery(
    Guid ParkingSpaceId,
    int HorizonHours = 24,
    int IntervalMinutes = 60) : IQuery<ApiResponse<ParkingAvailabilityForecastDto>>;

public sealed record GetOwnerParkingAvailabilityForecastsQuery(
    Guid OwnerId,
    int HorizonHours = 12,
    int IntervalMinutes = 60) : IQuery<ApiResponse<List<ParkingAvailabilityForecastDto>>>;

internal sealed class GetParkingAvailabilityForecastHandler
    : IQueryHandler<GetParkingAvailabilityForecastQuery, ApiResponse<ParkingAvailabilityForecastDto>>
{
    private readonly IParkingAvailabilityPredictionService _predictionService;

    public GetParkingAvailabilityForecastHandler(IParkingAvailabilityPredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    public Task<ApiResponse<ParkingAvailabilityForecastDto>> HandleAsync(
        GetParkingAvailabilityForecastQuery query,
        CancellationToken cancellationToken = default)
    {
        return _predictionService.GetForecastAsync(
            query.ParkingSpaceId,
            query.HorizonHours,
            query.IntervalMinutes,
            cancellationToken);
    }
}

internal sealed class GetOwnerParkingAvailabilityForecastsHandler
    : IQueryHandler<GetOwnerParkingAvailabilityForecastsQuery, ApiResponse<List<ParkingAvailabilityForecastDto>>>
{
    private readonly IParkingAvailabilityPredictionService _predictionService;

    public GetOwnerParkingAvailabilityForecastsHandler(IParkingAvailabilityPredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    public Task<ApiResponse<List<ParkingAvailabilityForecastDto>>> HandleAsync(
        GetOwnerParkingAvailabilityForecastsQuery query,
        CancellationToken cancellationToken = default)
    {
        return _predictionService.GetOwnerForecastsAsync(
            query.OwnerId,
            query.HorizonHours,
            query.IntervalMinutes,
            cancellationToken);
    }
}
