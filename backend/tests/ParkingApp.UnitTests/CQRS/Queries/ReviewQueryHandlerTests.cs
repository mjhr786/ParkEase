using ParkingApp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using ParkingApp.Marketplace.Application.Queries.Reviews;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Corporate.Domain;
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Marketplace.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Queries;

public class ReviewQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IReviewReadStore> _mockReadStore;
    private readonly Mock<ICacheService> _mockCache;

    public ReviewQueryHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockReviewRepo = new Mock<IReviewRepository>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockUow.Setup(u => u.Reviews).Returns(_mockReviewRepo.Object);
        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);

        _mockReadStore = new Mock<IReviewReadStore>();
        _mockCache = new Mock<ICacheService>();
    }

    // GetReviewByIdHandler Tests
    [Fact]
    public async Task GetReviewByIdHandler_ShouldFail_WhenNotFound()
    {
        var handler = new GetReviewByIdHandler(_mockUow.Object);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Review)null);

        var res = await handler.HandleAsync(new GetReviewByIdQuery(Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetReviewByIdHandler_ShouldSucceed()
    {
        var handler = new GetReviewByIdHandler(_mockUow.Object);
        var parkingId = Guid.NewGuid();
        var review = new Review { Id = Guid.NewGuid(), Title = "Good", ParkingSpaceId = parkingId };
        _mockReviewRepo.Setup(r => r.GetByIdAsync(review.Id, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = false });

        var res = await handler.HandleAsync(new GetReviewByIdQuery(review.Id));

        res.Success.Should().BeTrue();
        res.Data.Title.Should().Be("Good");
    }

    // GetReviewsByParkingSpaceHandler Tests
    [Fact]
    public async Task GetReviewsByParkingSpaceHandler_ShouldReturnFromCache()
    {
        var handler = new GetReviewsByParkingSpaceHandler(_mockUow.Object, _mockReadStore.Object, _mockCache.Object);
        var spaceId = Guid.NewGuid();
        var cacheKey = $"reviews:parking:{spaceId}";
        var cachedList = new List<ReviewDto> { new ReviewDto(Guid.NewGuid(), Guid.NewGuid(), "T", Guid.NewGuid(), null, 5, "C", "C", 0, "N", DateTime.UtcNow, DateTime.UtcNow) };

        _mockParkingRepo.Setup(r => r.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = spaceId, IsCorporateOnly = false });
        _mockCache.Setup(c => c.GetAsync<List<ReviewDto>>(cacheKey, It.IsAny<CancellationToken>())).ReturnsAsync(cachedList);

        var res = await handler.HandleAsync(new GetReviewsByParkingSpaceQuery(spaceId));

        res.Success.Should().BeTrue();
        res.Data!.Count.Should().Be(1);
        res.Data[0].Title.Should().Be("C");
        _mockReadStore.Verify(r => r.GetByParkingSpaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetReviewsByParkingSpaceHandler_ShouldLoadFromReadStore_WhenNotCached()
    {
        var handler = new GetReviewsByParkingSpaceHandler(_mockUow.Object, _mockReadStore.Object, _mockCache.Object);
        var spaceId = Guid.NewGuid();
        var list = new List<ReviewDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "User", spaceId, null, 4, "Nice", "Comment", 1, null, null, DateTime.UtcNow)
        };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = spaceId, IsCorporateOnly = false });
        _mockReadStore.Setup(r => r.GetByParkingSpaceAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var res = await handler.HandleAsync(new GetReviewsByParkingSpaceQuery(spaceId));

        res.Success.Should().BeTrue();
        res.Data.Should().HaveCount(1);
        _mockCache.Verify(c => c.SetAsync($"reviews:parking:{spaceId}", list, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}






