using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.UnitTests.Services;

public class ParkingAvailabilityPredictionServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IParkingSpaceRepository> _parkingRepositoryMock;
    private readonly Mock<IBookingRepository> _bookingRepositoryMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IParkingAvailabilityModelService> _modelServiceMock;
    private readonly ParkingAvailabilityPredictionService _service;

    public ParkingAvailabilityPredictionServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _parkingRepositoryMock = new Mock<IParkingSpaceRepository>();
        _bookingRepositoryMock = new Mock<IBookingRepository>();
        _cacheMock = new Mock<ICacheService>();
        _modelServiceMock = new Mock<IParkingAvailabilityModelService>();
        var loggerMock = new Mock<ILogger<ParkingAvailabilityPredictionService>>();

        _unitOfWorkMock.SetupGet(unitOfWork => unitOfWork.ParkingSpaces).Returns(_parkingRepositoryMock.Object);
        _unitOfWorkMock.SetupGet(unitOfWork => unitOfWork.Bookings).Returns(_bookingRepositoryMock.Object);
        _modelServiceMock
            .Setup(service => service.PredictOccupancyAsync(
                It.IsAny<ParkingAvailabilityModelInputDto>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingAvailabilityModelPredictionDto?)null);

        _service = new ParkingAvailabilityPredictionService(
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            _modelServiceMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task GetForecastAsync_ReturnsCachedForecast_WhenAvailable()
    {
        var parkingId = Guid.NewGuid();
        var cachedForecast = CreateForecast(parkingId, "Cached parking");

        _cacheMock
            .Setup(cache => cache.GetAsync<ParkingAvailabilityForecastDto>(
                $"parking-forecast:{parkingId}:24:60",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedForecast);

        var result = await _service.GetForecastAsync(parkingId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(parkingId, result.Data!.ParkingSpaceId);
        _parkingRepositoryMock.Verify(
            repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetForecastAsync_BuildsPredictionUsingExistingAndHistoricalBookings()
    {
        var now = DateTime.UtcNow;
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace
        {
            Id = parkingId,
            Title = "Central Plaza",
            TotalSpots = 10,
            IsActive = true
        };

        var bookings = new List<Booking>
        {
            new()
            {
                ParkingSpaceId = parkingId,
                StartDateTime = now.AddMinutes(-15),
                EndDateTime = now.AddMinutes(45),
                Status = BookingStatus.Confirmed
            },
            new()
            {
                ParkingSpaceId = parkingId,
                StartDateTime = now.AddDays(-7).AddMinutes(-10),
                EndDateTime = now.AddDays(-7).AddMinutes(50),
                Status = BookingStatus.Completed
            },
            new()
            {
                ParkingSpaceId = parkingId,
                StartDateTime = now.AddDays(-1).AddMinutes(-5),
                EndDateTime = now.AddDays(-1).AddMinutes(35),
                Status = BookingStatus.Completed
            }
        };

        _cacheMock
            .Setup(cache => cache.GetAsync<ParkingAvailabilityForecastDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingAvailabilityForecastDto?)null);
        _parkingRepositoryMock
            .Setup(repository => repository.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parking);
        _bookingRepositoryMock
            .Setup(repository => repository.GetForecastRelevantBookingsForSpacesAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings);

        var result = await _service.GetForecastAsync(parkingId, horizonHours: 4, intervalMinutes: 60);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(parkingId, result.Data!.ParkingSpaceId);
        Assert.Equal(4, result.Data.Buckets.Count);
        Assert.True(result.Data.CurrentPredictedBookedSpots >= 1);
        Assert.True(result.Data.CurrentPredictedAvailableSpots <= 9);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.CurrentAvailabilityBand));
    }

    [Fact]
    public async Task GetForecastAsync_UsesMachineLearningPrediction_WhenAvailable()
    {
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace
        {
            Id = parkingId,
            Title = "ML parking",
            TotalSpots = 10,
            IsActive = true
        };

        _cacheMock
            .Setup(cache => cache.GetAsync<ParkingAvailabilityForecastDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingAvailabilityForecastDto?)null);
        _parkingRepositoryMock
            .Setup(repository => repository.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parking);
        _bookingRepositoryMock
            .Setup(repository => repository.GetForecastRelevantBookingsForSpacesAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking>());
        _modelServiceMock
            .Setup(service => service.PredictOccupancyAsync(
                It.IsAny<ParkingAvailabilityModelInputDto>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingAvailabilityModelPredictionDto(0.9, 0.92, 1200, true));

        var result = await _service.GetForecastAsync(parkingId, horizonHours: 2, intervalMinutes: 60);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.CurrentPredictedBookedSpots >= 9);
        Assert.True(result.Data.CurrentConfidenceScore >= 0.8);
    }

    [Fact]
    public async Task GetOwnerForecastsAsync_ReturnsForecastsForOwnerListings()
    {
        var ownerId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        var parkingSpaces = new List<ParkingSpace>
        {
            new()
            {
                Id = parkingId,
                OwnerId = ownerId,
                Title = "Lake View",
                TotalSpots = 6,
                IsActive = true
            }
        };

        _cacheMock
            .Setup(cache => cache.GetAsync<List<ParkingAvailabilityForecastDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<ParkingAvailabilityForecastDto>?)null);
        _parkingRepositoryMock
            .Setup(repository => repository.GetByOwnerIdAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parkingSpaces);
        _bookingRepositoryMock
            .Setup(repository => repository.GetForecastRelevantBookingsForSpacesAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking>());

        var result = await _service.GetOwnerForecastsAsync(ownerId, horizonHours: 12, intervalMinutes: 60);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!);
        Assert.Equal(parkingId, result.Data[0].ParkingSpaceId);
    }

    private static ParkingAvailabilityForecastDto CreateForecast(Guid parkingId, string title)
    {
        var now = DateTime.UtcNow;
        return new ParkingAvailabilityForecastDto(
            parkingId,
            title,
            true,
            10,
            now,
            24,
            60,
            3,
            7,
            0.3,
            0.8,
            "Good",
            6,
            4,
            null,
            new List<ParkingAvailabilityBucketDto>
            {
                new(
                    now,
                    now.AddHours(1),
                    2,
                    3,
                    7,
                    0.25,
                    0.3,
                    0.8,
                    "Good",
                    true)
            });
    }
}
