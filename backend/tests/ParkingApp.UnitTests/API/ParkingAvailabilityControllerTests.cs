using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Queries.ParkingAvailability;
using ParkingApp.Application.DTOs;

namespace ParkingApp.UnitTests.API;

public class ParkingAvailabilityControllerTests
{
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly ParkingAvailabilityController _controller;

    public ParkingAvailabilityControllerTests()
    {
        _dispatcherMock = new Mock<IDispatcher>();
        _controller = new ParkingAvailabilityController(_dispatcherMock.Object);
    }

    [Fact]
    public async Task GetForecast_ReturnsOk_WhenForecastExists()
    {
        var parkingId = Guid.NewGuid();
        _dispatcherMock
            .Setup(dispatcher => dispatcher.QueryAsync(
                It.Is<GetParkingAvailabilityForecastQuery>(query =>
                    query.ParkingSpaceId == parkingId &&
                    query.HorizonHours == 24 &&
                    query.IntervalMinutes == 60),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ParkingAvailabilityForecastDto>(true, null, CreateForecast(parkingId), null));

        var result = await _controller.GetForecast(parkingId, 24, 60, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetMyListingsForecasts_ReturnsOk_ForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(userId);

        _dispatcherMock
            .Setup(dispatcher => dispatcher.QueryAsync(
                It.Is<GetOwnerParkingAvailabilityForecastsQuery>(query =>
                    query.OwnerId == userId &&
                    query.HorizonHours == 12 &&
                    query.IntervalMinutes == 60),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<List<ParkingAvailabilityForecastDto>>(
                true,
                null,
                new List<ParkingAvailabilityForecastDto> { CreateForecast(Guid.NewGuid()) },
                null));

        var result = await _controller.GetMyListingsForecasts(12, 60, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    private void SetupControllerUser(Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "User")
        };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
    }

    private static ParkingAvailabilityForecastDto CreateForecast(Guid parkingId)
    {
        var now = DateTime.UtcNow;
        return new ParkingAvailabilityForecastDto(
            parkingId,
            "Controller listing",
            true,
            5,
            now,
            12,
            60,
            1,
            4,
            0.2,
            0.8,
            "High",
            4,
            2,
            null,
            new List<ParkingAvailabilityBucketDto>
            {
                new(
                    now,
                    now.AddHours(1),
                    1,
                    1,
                    4,
                    0.2,
                    0.2,
                    0.8,
                    "High",
                    true)
            });
    }
}
