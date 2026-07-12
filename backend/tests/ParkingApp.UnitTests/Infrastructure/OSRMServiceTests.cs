using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
        var handlerMock = new Mock<HttpMessageHandler>();
        var response = new
        {
            code = "Ok",
            distances = new[]
            {
                new double?[] { 1000.0, 5500.0 }
            },
            durations = new[]
            {
                new double?[] { 120.0, 600.0 }
            }
        };

        handlerMock
           .Protected()
           .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
           .ReturnsAsync(new HttpResponseMessage
           {
               StatusCode = HttpStatusCode.OK,
               Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
           });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://router.project-osrm.org/")
        };
        var service = new OSRMService(httpClient, _loggerMock.Object);

        var destinations = new List<(double Lat, double Lng)>
        {
            (12.9750, 77.5950),
            (13.0000, 77.6000)
        };

        var results = await service.GetBatchRoutingAsync(12.9716, 77.5946, destinations);

        Assert.Equal(2, results.Count);
        Assert.Equal(1.0, results[0].Distance);
        Assert.Equal(2, results[0].Duration);
        Assert.Equal(5.5, results[1].Distance);
        Assert.Equal(10, results[1].Duration);
    }

    [Fact]
    public async Task GetBatchRoutingAsync_OnApiError_FallsBackToHaversine()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
           .Protected()
           .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
           .ReturnsAsync(new HttpResponseMessage
           {
               StatusCode = HttpStatusCode.BadRequest,
               Content = new StringContent("""{"message":"URL string malformed","code":"InvalidUrl"}""")
           });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://router.project-osrm.org/")
        };
        var service = new OSRMService(httpClient, _loggerMock.Object);

        var destinations = new List<(double Lat, double Lng)> { (12.9750, 77.5950) };

        var results = await service.GetBatchRoutingAsync(12.9716, 77.5946, destinations);

        Assert.Single(results);
        Assert.True(results[0].Distance > 0);
        Assert.True(results[0].Duration > 0);
    }

    [Fact]
    public async Task GetBatchRoutingAsync_SkipsInvalidZeroCoordinates_DoesNotCallOsrm()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://router.project-osrm.org/")
        };
        var service = new OSRMService(httpClient, _loggerMock.Object);

        var destinations = new List<(double Lat, double Lng)>
        {
            (0, 0),
            (0.0, 0.0)
        };

        var results = await service.GetBatchRoutingAsync(12.9716, 77.5946, destinations);

        Assert.Equal(2, results.Count);
        Assert.All(results, r =>
        {
            Assert.Equal(0.0, r.Distance);
            Assert.Equal(0, r.Duration);
        });
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetBatchRoutingAsync_FiltersInvalidDestinations_AndRoutesValidOnes()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var response = new
        {
            code = "Ok",
            distances = new[] { new double?[] { 1000.0 } },
            durations = new[] { new double?[] { 120.0 } }
        };

        handlerMock
           .Protected()
           .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
           .ReturnsAsync(new HttpResponseMessage
           {
               StatusCode = HttpStatusCode.OK,
               Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
           });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://router.project-osrm.org/")
        };
        var service = new OSRMService(httpClient, _loggerMock.Object);

        var destinations = new List<(double Lat, double Lng)>
        {
            (0, 0),                 // invalid — must not be sent
            (12.9750, 77.5950)      // valid
        };

        var results = await service.GetBatchRoutingAsync(12.9716, 77.5946, destinations);

        Assert.Equal(2, results.Count);
        Assert.Equal(0.0, results[0].Distance);
        Assert.Equal(1.0, results[1].Distance);
        Assert.Equal(2, results[1].Duration);
    }

}
