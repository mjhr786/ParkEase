using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.Application.Caching;
using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.UnitTests.Services;

public class ParkingAvailabilityPredictionServiceTests
{
    private readonly Mock<IMarketplaceUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IParkingSpaceRepository> _parkingRepositoryMock;
    private readonly Mock<IBookingRepository> _bookingRepositoryMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IParkingAvailabilityModelService> _modelServiceMock;
    private ForecastOptions _forecastOptions = new() { Enabled = true, EnableMl = false };

    public ParkingAvailabilityPredictionServiceTests()
    {
        _unitOfWorkMock = new Mock<IMarketplaceUnitOfWork>();
        _parkingRepositoryMock = new Mock<IParkingSpaceRepository>();
        _bookingRepositoryMock = new Mock<IBookingRepository>();
        _cacheMock = new Mock<ICacheService>();
        _modelServiceMock = new Mock<IParkingAvailabilityModelService>();

        _unitOfWorkMock.SetupGet(unitOfWork => unitOfWork.ParkingSpaces).Returns(_parkingRepositoryMock.Object);
        _unitOfWorkMock.SetupGet(unitOfWork => unitOfWork.Bookings).Returns(_bookingRepositoryMock.Object);
        _modelServiceMock
            .Setup(service => service.PredictOccupancyAsync(
                It.IsAny<ParkingAvailabilityModelInputDto>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingAvailabilityModelPredictionDto?)null);
    }

    private ParkingAvailabilityPredictionService CreateService()
    {
        var loggerMock = new Mock<ILogger<ParkingAvailabilityPredictionService>>();
        return new ParkingAvailabilityPredictionService(
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            _modelServiceMock.Object,
            new TestOptionsMonitor(_forecastOptions),
            loggerMock.Object);
    }

    [Fact]
    public async Task GetForecastAsync_ReturnsCachedForecast_WhenAvailable()
    {
        var service = CreateService();
        var parkingId = Guid.NewGuid();
        var cachedForecast = CreateForecast(parkingId, "Cached parking");
        var cacheKey = CacheKeys.ParkingForecast(parkingId, 24, 60, mlEnabled: false);

        _cacheMock
            .Setup(cache => cache.GetAsync<ParkingAvailabilityForecastDto>(
                cacheKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedForecast);

        var result = await service.GetForecastAsync(parkingId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(parkingId, result.Data!.ParkingSpaceId);
        _parkingRepositoryMock.Verify(
            repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetForecastAsync_WhenForecastDisabled_ReturnsDisabledWithoutWork()
    {
        _forecastOptions = new ForecastOptions { Enabled = false, EnableMl = true };
        var service = CreateService();
        var parkingId = Guid.NewGuid();

        var result = await service.GetForecastAsync(parkingId);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
        _parkingRepositoryMock.Verify(
            repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _modelServiceMock.Verify(
            model => model.PredictOccupancyAsync(
                It.IsAny<ParkingAvailabilityModelInputDto>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _cacheMock.Verify(
            cache => cache.GetAsync<ParkingAvailabilityForecastDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOwnerForecastsAsync_WhenForecastDisabled_ReturnsEmptyList()
    {
        _forecastOptions = new ForecastOptions { Enabled = false, EnableMl = true };
        var service = CreateService();

        var result = await service.GetOwnerForecastsAsync(Guid.NewGuid());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data!);
        _parkingRepositoryMock.Verify(
            repository => repository.GetByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _modelServiceMock.Verify(
            model => model.PredictOccupancyAsync(
                It.IsAny<ParkingAvailabilityModelInputDto>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetForecastAsync_WhenMlDisabled_DoesNotCallModelService()
    {
        _forecastOptions = new ForecastOptions { Enabled = true, EnableMl = false };
        var service = CreateService();
        var parkingId = Guid.NewGuid();
        var parking = new ParkingSpace
        {
            Id = parkingId,
            Title = "Deterministic only",
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

        var result = await service.GetForecastAsync(parkingId, horizonHours: 2, intervalMinutes: 60);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        _modelServiceMock.Verify(
            service => service.PredictOccupancyAsync(
                It.IsAny<ParkingAvailabilityModelInputDto>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetForecastAsync_BuildsPredictionUsingExistingAndHistoricalBookings()
    {
        var service = CreateService();
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

        var result = await service.GetForecastAsync(parkingId, horizonHours: 4, intervalMinutes: 60);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(parkingId, result.Data!.ParkingSpaceId);
        Assert.Equal(4, result.Data.Buckets.Count);
        Assert.True(result.Data.CurrentPredictedBookedSpots >= 1);
        Assert.True(result.Data.CurrentPredictedAvailableSpots <= 9);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.CurrentAvailabilityBand));
    }

    [Fact]
    public async Task GetForecastAsync_UsesMachineLearningPrediction_WhenEnableMlTrue()
    {
        _forecastOptions = new ForecastOptions { Enabled = true, EnableMl = true };
        var service = CreateService();
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

        var result = await service.GetForecastAsync(parkingId, horizonHours: 2, intervalMinutes: 60);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.CurrentPredictedBookedSpots >= 9);
        Assert.True(result.Data.CurrentConfidenceScore >= 0.8);
        _modelServiceMock.Verify(
            service => service.PredictOccupancyAsync(
                It.IsAny<ParkingAvailabilityModelInputDto>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetOwnerForecastsAsync_ReturnsForecastsForOwnerListings()
    {
        var service = CreateService();
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

        var result = await service.GetOwnerForecastsAsync(ownerId, horizonHours: 12, intervalMinutes: 60);

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

    private sealed class TestOptionsMonitor : IOptionsMonitor<ForecastOptions>
    {
        public TestOptionsMonitor(ForecastOptions current) => CurrentValue = current;
        public ForecastOptions CurrentValue { get; }
        public ForecastOptions Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<ForecastOptions, string?> listener) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
