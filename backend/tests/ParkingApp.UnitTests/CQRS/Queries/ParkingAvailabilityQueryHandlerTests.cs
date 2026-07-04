using Moq;
using ParkingApp.Application.CQRS.Queries.ParkingAvailability;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.UnitTests.CQRS.Queries;

public class ParkingAvailabilityQueryHandlerTests
{
    [Fact]
    public async Task GetParkingAvailabilityForecastHandler_DelegatesToPredictionService()
    {
        var parkingId = Guid.NewGuid();
        var serviceMock = new Mock<IParkingAvailabilityPredictionService>();
        var expected = new ApiResponse<ParkingAvailabilityForecastDto>(true, null, CreateForecast(parkingId), null);

        serviceMock
            .Setup(service => service.GetForecastAsync(parkingId, 24, 60, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetParkingAvailabilityForecastHandler(serviceMock.Object);

        var result = await handler.HandleAsync(new GetParkingAvailabilityForecastQuery(parkingId, 24, 60));

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetOwnerParkingAvailabilityForecastsHandler_DelegatesToPredictionService()
    {
        var ownerId = Guid.NewGuid();
        var serviceMock = new Mock<IParkingAvailabilityPredictionService>();
        var expected = new ApiResponse<List<ParkingAvailabilityForecastDto>>(
            true,
            null,
            new List<ParkingAvailabilityForecastDto> { CreateForecast(Guid.NewGuid()) },
            null);

        serviceMock
            .Setup(service => service.GetOwnerForecastsAsync(ownerId, 12, 60, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetOwnerParkingAvailabilityForecastsHandler(serviceMock.Object);

        var result = await handler.HandleAsync(new GetOwnerParkingAvailabilityForecastsQuery(ownerId, 12, 60));

        Assert.Same(expected, result);
    }

    private static ParkingAvailabilityForecastDto CreateForecast(Guid parkingId)
    {
        var now = DateTime.UtcNow;
        return new ParkingAvailabilityForecastDto(
            parkingId,
            "Forecast listing",
            true,
            8,
            now,
            12,
            60,
            2,
            6,
            0.25,
            0.82,
            "Good",
            5,
            4,
            null,
            new List<ParkingAvailabilityBucketDto>
            {
                new(
                    now,
                    now.AddHours(1),
                    1,
                    2,
                    6,
                    0.2,
                    0.25,
                    0.82,
                    "Good",
                    true)
            });
    }
}
