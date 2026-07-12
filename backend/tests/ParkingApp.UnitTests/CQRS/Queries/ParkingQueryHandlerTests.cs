using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.CQRS.Queries.Parking;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Queries;

public class ParkingQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<IParkingReadStore> _mockReadStore;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<IRoutingService> _mockRouting;
    
    public ParkingQueryHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockReadStore = new Mock<IParkingReadStore>();

        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        
        _mockCache = new Mock<ICacheService>();
        _mockRouting = new Mock<IRoutingService>();
    }

    // GetParkingByIdHandler Tests
    [Fact]
    public async Task GetParkingByIdHandler_ShouldReturnFromCache()
    {
        var logger = new Mock<ILogger<GetParkingByIdHandler>>();
        var handler = new GetParkingByIdHandler(_mockUow.Object, _mockCache.Object, logger.Object);
        var parkingId = Guid.NewGuid();
        var cacheKey = $"parking:{parkingId}";
        var cachedDto = new ParkingSpaceDto(parkingId, Guid.NewGuid(), "T", "D", "A", "C", "S", "C", "P", "10", 1.0, 1.0, ParkingApp.Domain.Enums.ParkingType.Open, 1, 1, 10m, 0m, 10m, 10m, TimeSpan.Zero, TimeSpan.Zero, true, new List<string>(), new List<ParkingApp.Domain.Enums.VehicleType>(), new List<string>(), true, true, 5.0, 10, null, DateTime.UtcNow, null);
        
        _mockCache.Setup(c => c.GetAsync<ParkingSpaceDto>(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(cachedDto);

        var res = await handler.HandleAsync(new GetParkingByIdQuery(parkingId));

        res.Success.Should().BeTrue();
        res.Data.Id.Should().Be(parkingId);
        _mockParkingRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetParkingByIdHandler_ShouldFail_WhenNotFound()
    {
        var logger = new Mock<ILogger<GetParkingByIdHandler>>();
        var handler = new GetParkingByIdHandler(_mockUow.Object, _mockCache.Object, logger.Object);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace)null);

        var res = await handler.HandleAsync(new GetParkingByIdQuery(Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not found");
    }

    // GetOwnerParkingsHandler Tests
    [Fact]
    public async Task GetOwnerParkingsHandler_ShouldSucceed()
    {
        var handler = new GetOwnerParkingsHandler(_mockUow.Object, new Mock<ParkingApp.Application.Interfaces.ICacheService>().Object);
        var ownerId = Guid.NewGuid();
        var spaces = new List<ParkingSpace> { new ParkingSpace { Id = Guid.NewGuid(), OwnerId = ownerId } };
        _mockParkingRepo.Setup(r => r.GetByOwnerIdAsync(ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(spaces);

        var res = await handler.HandleAsync(new GetOwnerParkingsQuery(ownerId));

        res.Success.Should().BeTrue();
        res.Data.Count.Should().Be(1);
    }

    // SearchParkingHandler Tests
    [Fact]
    public async Task SearchParkingHandler_ShouldReturnFromCache()
    {
        var logger = new Mock<ILogger<SearchParkingHandler>>();
        var handler = new SearchParkingHandler(_mockUow.Object, _mockReadStore.Object, _mockCache.Object, _mockRouting.Object, logger.Object);
        var searchDto = new ParkingSearchDto();
        var cacheKey = $"search:{searchDto.State}:{searchDto.City}:{searchDto.Address}:{searchDto.ParkingType}:{searchDto.VehicleType}:{searchDto.MinPrice}:{searchDto.MaxPrice}::{searchDto.Page}:{searchDto.PageSize}";
        var cachedResult = new ParkingSearchResultDto(new List<ParkingSpaceDto>(), 0, 1, 10, 0);
        
        _mockCache.Setup(c => c.GetAsync<ParkingSearchResultDto>(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(cachedResult);

        var res = await handler.HandleAsync(new SearchParkingQuery(searchDto));

        res.Success.Should().BeTrue();
        _mockReadStore.Verify(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchParkingHandler_ShouldSucceed()
    {
        var logger = new Mock<ILogger<SearchParkingHandler>>();
        var handler = new SearchParkingHandler(_mockUow.Object, _mockReadStore.Object, _mockCache.Object, _mockRouting.Object, logger.Object);
        var searchDto = new ParkingSearchDto { City = "TestCity" };
        var parkingId = Guid.NewGuid();
        var spaces = new List<ParkingSpace> { new ParkingSpace { Id = parkingId, Title = "A", TotalSpots = 1 } };
        
        _mockReadStore.Setup(r => r.SearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(spaces);
        _mockReadStore.Setup(r => r.CountSearchAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockBookingRepo.Setup(r => r.GetActiveBookingsForSpacesAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Booking>());

        var res = await handler.HandleAsync(new SearchParkingQuery(searchDto));

        res.Success.Should().BeTrue();
        res.Data!.TotalCount.Should().Be(1);
    }

    // GetMapCoordinatesHandler Tests
    [Fact]
    public async Task GetMapCoordinatesHandler_ShouldReturnFromCache()
    {
        var handler = new GetMapCoordinatesHandler(_mockReadStore.Object, _mockCache.Object);
        var searchDto = new ParkingSearchDto();
        var cacheKey = $"map:{searchDto.State}:{searchDto.City}:{searchDto.Address}:{searchDto.ParkingType}:{searchDto.VehicleType}:{searchDto.MinPrice}:{searchDto.MaxPrice}:{searchDto.RadiusKm}:{searchDto.Latitude}:{searchDto.Longitude}:";
        var cachedResult = new List<ParkingMapDto> { new ParkingMapDto(Guid.NewGuid(), "A", "A", "C", 1, 1, 1, null, 1, ParkingApp.Domain.Enums.ParkingType.Open) };
        
        _mockCache.Setup(c => c.GetAsync<List<ParkingMapDto>>(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(cachedResult);

        var res = await handler.HandleAsync(new GetMapCoordinatesQuery(searchDto));

        res.Success.Should().BeTrue();
        res.Data!.Count.Should().Be(1);
        _mockReadStore.Verify(r => r.GetMapPinsAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMapCoordinatesHandler_ShouldLoadFromReadStore_WhenNotCached()
    {
        var handler = new GetMapCoordinatesHandler(_mockReadStore.Object, _mockCache.Object);
        var searchDto = new ParkingSearchDto { City = "Mumbai" };
        var pins = new List<ParkingMapDto>
        {
            new(Guid.NewGuid(), "Pin", "Addr", "Mumbai", 19, 72, 50, null, 4.5, ParkingApp.Domain.Enums.ParkingType.Open)
        };
        _mockReadStore.Setup(r => r.GetMapPinsAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pins);

        var res = await handler.HandleAsync(new GetMapCoordinatesQuery(searchDto));

        res.Success.Should().BeTrue();
        res.Data.Should().HaveCount(1);
        _mockReadStore.Verify(r => r.GetMapPinsAsync(It.IsAny<ParkingSearchDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
