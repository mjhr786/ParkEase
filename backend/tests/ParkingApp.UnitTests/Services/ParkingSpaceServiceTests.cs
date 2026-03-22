using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Models;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class ParkingSpaceServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IParkingSpaceRepository> _mockRepo;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<ParkingSpaceService>> _mockLogger;
    private readonly ParkingSpaceService _service;

    public ParkingSpaceServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockRepo = new Mock<IParkingSpaceRepository>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        
        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockRepo.Object);
        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);
        
        _mockCache = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<ParkingSpaceService>>();

        _service = new ParkingSpaceService(_mockUow.Object, _mockCache.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFromCache()
    {
        var id = Guid.NewGuid();
        var dto = new ParkingSpaceDto(id, Guid.NewGuid(), "T", "D", "A", "C", "S", "CO", "P", "12345", 1.0, 1.0, ParkingType.Open, 1, 1, 1m, 1m, 1m, 1m, new TimeSpan(), new TimeSpan(), false, new List<string>(), new List<VehicleType>(), new List<string>(), true, true, 5.0, 10, null, DateTime.UtcNow, null);
        _mockCache.Setup(c => c.GetAsync<ParkingSpaceDto>($"parking:{id}", It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var res = await _service.GetByIdAsync(id);

        res.Success.Should().BeTrue();
        res.Data.Should().Be(dto);
        _mockRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldFetchAndCache_WhenNotCached()
    {
        var id = Guid.NewGuid();
        var parking = new ParkingSpace { Id = id };
        _mockCache.Setup(c => c.GetAsync<ParkingSpaceDto>($"parking:{id}", It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpaceDto?)null);
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var res = await _service.GetByIdAsync(id);

        res.Success.Should().BeTrue();
        res.Data!.Id.Should().Be(id);
        _mockCache.Verify(c => c.SetAsync($"parking:{id}", It.IsAny<ParkingSpaceDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnFromCache()
    {
        var dto = new ParkingSearchDto();
        var resultDto = new ParkingSearchResultDto(new List<ParkingSpaceDto>(), 0, 1, 10, 0);
        _mockCache.Setup(c => c.GetAsync<ParkingSearchResultDto>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(resultDto);

        var res = await _service.SearchAsync(dto);

        res.Success.Should().BeTrue();
        res.Data.Should().Be(resultDto);
        _mockRepo.Verify(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_ShouldQueryAndCache()
    {
        var dto = new ParkingSearchDto { Page = 1, PageSize = 10 };
        _mockCache.Setup(c => c.GetAsync<ParkingSearchResultDto>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSearchResultDto?)null);
        
        var parkingSpaces = new List<ParkingSpace> { new ParkingSpace { Id = Guid.NewGuid() } };
        _mockRepo.Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(parkingSpaces);
        _mockBookingRepo.Setup(r => r.GetActiveBookingsForSpacesAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Booking>());

        var res = await _service.SearchAsync(dto);

        res.Success.Should().BeTrue();
        res.Data!.ParkingSpaces.Should().HaveCount(1);
        _mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<ParkingSearchResultDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByOwnerAsync_ShouldReturnList()
    {
        var ownerId = Guid.NewGuid();
        var spaces = new List<ParkingSpace> { new ParkingSpace { Id = Guid.NewGuid() } };
        _mockRepo.Setup(r => r.GetByOwnerIdAsync(ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(spaces);

        var res = await _service.GetByOwnerAsync(ownerId);

        res.Success.Should().BeTrue();
        res.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_ShouldFail_WhenOwnerNotFound()
    {
        var ownerId = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(ownerId, It.IsAny<CancellationToken>())).ReturnsAsync((User)null);

        var res = await _service.CreateAsync(ownerId, new CreateParkingSpaceDto("T", "D", "A", "C", "S", "C", "P", 1, 1, ParkingType.Open, 1, 1, 1, 1, 1, TimeSpan.Zero, TimeSpan.Zero, false, new List<string>(), new List<VehicleType>(), new List<string>(), "S"));

        res.Success.Should().BeFalse();
        res.Message.Should().Be("Owner not found");
    }

    [Fact]
    public async Task CreateAsync_ShouldSucceed()
    {
        var ownerId = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(new User { Role = UserRole.Vendor });

        var res = await _service.CreateAsync(ownerId, new CreateParkingSpaceDto("Title", "Description", "Address", "City", "State", "Country", "PostalCode", 1.0, 1.0, ParkingType.Open, 1, 1.0m, 1.0m, 1.0m, 1.0m, TimeSpan.Zero, TimeSpan.Zero, false, new List<string>(), new List<VehicleType>(), new List<string>(), "Instructions"));

        res.Success.Should().BeTrue();
        _mockRepo.Verify(r => r.AddAsync(It.IsAny<ParkingSpace>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveByPatternAsync("search:*", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenNotFound()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace?)null);

        var res = await _service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateParkingSpaceDto("T", "D", "A", "C", "S", "C", "P", 1, 1, ParkingType.Open, 1, 1, 1, 1, 1, TimeSpan.Zero, TimeSpan.Zero, false, new List<string>(), new List<VehicleType>(), new List<string>(), "S", true));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenUnauthorized()
    {
        var space = new ParkingSpace { OwnerId = Guid.NewGuid() };
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var res = await _service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateParkingSpaceDto("T", "D", "A", "C", "S", "C", "P", 1, 1, ParkingType.Open, 1, 1, 1, 1, 1, TimeSpan.Zero, TimeSpan.Zero, false, new List<string>(), new List<VehicleType>(), new List<string>(), "S", true));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ShouldSucceed()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var space = new ParkingSpace { Id = id, OwnerId = ownerId };
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var res = await _service.UpdateAsync(id, ownerId, new UpdateParkingSpaceDto("T", "D", "A", "C", "S", "C", "P", 1, 1, ParkingType.Open, 1, 1, 1, 1, 1, TimeSpan.Zero, TimeSpan.Zero, false, new List<string>(), new List<VehicleType>(), new List<string>(), "S", true));

        res.Success.Should().BeTrue();
        _mockRepo.Verify(r => r.Update(space), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"parking:{id}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenActiveBookingsExist()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var space = new ParkingSpace { Id = id, OwnerId = ownerId };
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _mockBookingRepo.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Booking, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var res = await _service.DeleteAsync(id, ownerId);

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("active bookings");
    }

    [Fact]
    public async Task DeleteAsync_ShouldSucceed()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var space = new ParkingSpace { Id = id, OwnerId = ownerId };
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(space);
        _mockBookingRepo.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Booking, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var res = await _service.DeleteAsync(id, ownerId);

        res.Success.Should().BeTrue();
        _mockRepo.Verify(r => r.Remove(space), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleActiveAsync_ShouldSucceed()
    {
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var space = new ParkingSpace { Id = id, OwnerId = ownerId, IsActive = true };
        _mockRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var res = await _service.ToggleActiveAsync(id, ownerId);

        res.Success.Should().BeTrue();
        space.IsActive.Should().BeFalse();
        _mockRepo.Verify(r => r.Update(space), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMapCoordinatesAsync_ShouldReturnFromCache()
    {
        var dto = new ParkingSearchDto();
        var mapDtos = new List<ParkingMapDto>();
        _mockCache.Setup(c => c.GetAsync<List<ParkingMapDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(mapDtos);

        var res = await _service.GetMapCoordinatesAsync(dto);

        res.Success.Should().BeTrue();
        res.Data.Should().BeSameAs(mapDtos);
        _mockRepo.Verify(r => r.GetMapCoordinatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMapCoordinatesAsync_ShouldQueryAndCache()
    {
        var dto = new ParkingSearchDto();
        _mockCache.Setup(c => c.GetAsync<List<ParkingMapDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((List<ParkingMapDto>?)null);
        
        var spaces = new List<ParkingMapModel> { new ParkingMapModel(Guid.NewGuid(), "T", "A", "C", 1, 1, 1, null, 1, ParkingType.Open) };
        _mockRepo.Setup(r => r.GetMapCoordinatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double?>(), It.IsAny<CancellationToken>())).ReturnsAsync(spaces);

        var res = await _service.GetMapCoordinatesAsync(dto);

        res.Success.Should().BeTrue();
        res.Data.Should().HaveCount(1);
        _mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<ParkingMapDto>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
