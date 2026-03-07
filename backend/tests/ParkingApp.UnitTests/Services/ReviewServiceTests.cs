using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class ReviewServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<ReviewService>> _mockLogger;
    private readonly ReviewService _service;

    public ReviewServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockReviewRepo = new Mock<IReviewRepository>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();

        _mockUow.Setup(u => u.Reviews).Returns(_mockReviewRepo.Object);
        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);

        _mockCache = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<ReviewService>>();

        _service = new ReviewService(_mockUow.Object, _mockCache.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFail_WhenNotFound()
    {
        _mockReviewRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Review)null);
        var res = await _service.GetByIdAsync(Guid.NewGuid());
        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetByParkingSpaceAsync_ShouldReturnFromCache()
    {
        var pid = Guid.NewGuid();
        var dtos = new List<ReviewDto>();
        _mockCache.Setup(c => c.GetAsync<List<ReviewDto>>($"reviews:parking:{pid}", It.IsAny<CancellationToken>())).ReturnsAsync(dtos);

        var res = await _service.GetByParkingSpaceAsync(pid);

        res.Success.Should().BeTrue();
        res.Data.Should().BeSameAs(dtos);
        _mockReviewRepo.Verify(r => r.GetByParkingSpaceIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnFail_WhenParkingNotFound()
    {
        _mockParkingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace)null);
        var res = await _service.CreateAsync(Guid.NewGuid(), new CreateReviewDto(Guid.NewGuid(), null, 5, "T", "C"));
        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ShouldSucceed()
    {
        var pid = Guid.NewGuid();
        var parking = new ParkingSpace { Id = pid, TotalReviews = 0, AverageRating = 0 };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(pid, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockReviewRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Review, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((Review?)null);
        _mockReviewRepo.Setup(r => r.GetAverageRatingAsync(pid, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var res = await _service.CreateAsync(Guid.NewGuid(), new CreateReviewDto(pid, null, 5, "T", "C"));

        res.Success.Should().BeTrue();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"reviews:parking:{pid}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldSucceed()
    {
        var uid = Guid.NewGuid();
        var pid = Guid.NewGuid();
        var review = new Review { Id = Guid.NewGuid(), UserId = uid, ParkingSpaceId = pid, Rating = 5 };
        var parking = new ParkingSpace { Id = pid, TotalReviews = 1, AverageRating = 5 };
        
        _mockReviewRepo.Setup(r => r.GetByIdAsync(review.Id, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(pid, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var res = await _service.DeleteAsync(review.Id, uid);

        res.Success.Should().BeTrue();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        parking.TotalReviews.Should().Be(0);
        parking.AverageRating.Should().Be(0);
    }
}
