using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ParkingApp.Infrastructure.Services;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure;

public class OSRMServiceTests
{
    private readonly Mock<ILogger<OSRMService>> _loggerMock;

    public OSRMServiceTests()
    {
        _loggerMock = new Mock<ILogger<OSRMService>>();
    }

    [Fact]
    public async Task GetBatchRoutingAsync_ReturnsCorrectDistanceAndDuration()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var response = new
        {
            code = "Ok",
            distances = new[]
            {
                new[] { 0.0, 1000.0, 5500.0 }
            },
            durations = new[]
            {
                new[] { 0.0, 120.0, 600.0 }
            }
        };

        handlerMock
           .Protected()
           .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>()
           )
           .ReturnsAsync(new HttpResponseMessage
           {
               StatusCode = HttpStatusCode.OK,
               Content = JsonContent.Create(response)
           });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new OSRMService(httpClient, _loggerMock.Object);

        var startLat = 12.9716;
        var startLng = 77.5946;
        var destinations = new List<(double Lat, double Lng)>
        {
            (12.9750, 77.5950), // ~1km
            (13.0000, 77.6000)  // ~5.5km
        };

        // Act
        var results = await service.GetBatchRoutingAsync(startLat, startLng, destinations);

        // Assert
        Assert.Equal(2, results.Count);
        
        // Dest 1: 1000m -> 1.0km, 120s -> 2 mins
        Assert.Equal(1.0, results[0].Distance);
        Assert.Equal(2, results[0].Duration);

        // Dest 2: 5500m -> 5.5km, 600s -> 10 mins
        Assert.Equal(5.5, results[1].Distance);
        Assert.Equal(10, results[1].Duration);
    }

    [Fact]
    public async Task GetBatchRoutingAsync_OnApiError_ReturnsEmptyFallback()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
           .Protected()
           .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>()
           )
           .ReturnsAsync(new HttpResponseMessage
           {
               StatusCode = HttpStatusCode.InternalServerError
           });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new OSRMService(httpClient, _loggerMock.Object);

        var destinations = new List<(double Lat, double Lng)> { (12.0, 77.0) };

        // Act
        var results = await service.GetBatchRoutingAsync(0, 0, destinations);

        // Assert
        Assert.Single(results);
        Assert.Equal(0.0, results[0].Distance);
        Assert.Equal(0, results[0].Duration);
    }
}
