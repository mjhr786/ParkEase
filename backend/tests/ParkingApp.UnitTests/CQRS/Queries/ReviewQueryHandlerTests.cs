using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Queries.Reviews;
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

public class ReviewQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IReviewReadStore> _mockReadStore;
    private readonly Mock<ICacheService> _mockCache;

    public ReviewQueryHandlerTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockReviewRepo = new Mock<IReviewRepository>();
        _mockUow.Setup(u => u.Reviews).Returns(_mockReviewRepo.Object);

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
        var review = new Review { Id = Guid.NewGuid(), Title = "Good" };
        _mockReviewRepo.Setup(r => r.GetByIdAsync(review.Id, It.IsAny<CancellationToken>())).ReturnsAsync(review);

        var res = await handler.HandleAsync(new GetReviewByIdQuery(review.Id));

        res.Success.Should().BeTrue();
        res.Data.Title.Should().Be("Good");
    }

    // GetReviewsByParkingSpaceHandler Tests
    [Fact]
    public async Task GetReviewsByParkingSpaceHandler_ShouldReturnFromCache()
    {
        var handler = new GetReviewsByParkingSpaceHandler(_mockReadStore.Object, _mockCache.Object);
        var spaceId = Guid.NewGuid();
        var cacheKey = $"reviews:parking:{spaceId}";
        var cachedList = new List<ReviewDto> { new ReviewDto(Guid.NewGuid(), Guid.NewGuid(), "T", Guid.NewGuid(), null, 5, "C", "C", 0, "N", DateTime.UtcNow, DateTime.UtcNow) };
        
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
        var handler = new GetReviewsByParkingSpaceHandler(_mockReadStore.Object, _mockCache.Object);
        var spaceId = Guid.NewGuid();
        var list = new List<ReviewDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "User", spaceId, null, 4, "Nice", "Comment", 1, null, null, DateTime.UtcNow)
        };
        _mockReadStore.Setup(r => r.GetByParkingSpaceAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var res = await handler.HandleAsync(new GetReviewsByParkingSpaceQuery(spaceId));

        res.Success.Should().BeTrue();
        res.Data.Should().HaveCount(1);
        _mockCache.Verify(c => c.SetAsync($"reviews:parking:{spaceId}", list, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
