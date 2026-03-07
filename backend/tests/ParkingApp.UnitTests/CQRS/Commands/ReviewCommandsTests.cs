using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.CQRS.Commands.Reviews;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class ReviewCommandsTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<CreateReviewHandler>> _mockCreateLogger;

    public ReviewCommandsTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockReviewRepo = new Mock<IReviewRepository>();

        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUow.Setup(u => u.Reviews).Returns(_mockReviewRepo.Object);

        _mockCache = new Mock<ICacheService>();
        _mockCreateLogger = new Mock<ILogger<CreateReviewHandler>>();
    }

    // CreateReviewHandler Tests
    [Fact]
    public async Task CreateReviewHandler_ShouldFail_WhenParkingNotFound()
    {
        var handler = new CreateReviewHandler(_mockUow.Object, _mockCache.Object, _mockCreateLogger.Object);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace)null);

        var res = await handler.HandleAsync(new CreateReviewCommand(Guid.NewGuid(), new CreateReviewDto(Guid.NewGuid(), null, 5, "T", "C")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Parking space not found");
    }

    [Fact]
    public async Task CreateReviewHandler_ShouldSucceed()
    {
        var handler = new CreateReviewHandler(_mockUow.Object, _mockCache.Object, _mockCreateLogger.Object);
        var parkingSpaceId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingSpaceId, TotalReviews = 0, AverageRating = 0 };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingSpaceId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockReviewRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Review, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((Review)null);
        _mockReviewRepo.Setup(r => r.GetAverageRatingAsync(parkingSpaceId, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var res = await handler.HandleAsync(new CreateReviewCommand(Guid.NewGuid(), new CreateReviewDto(parkingSpaceId, null, 5, "T", "C")));

        res.Success.Should().BeTrue();
        parking.TotalReviews.Should().Be(1);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UpdateReviewHandler Tests
    [Fact]
    public async Task UpdateReviewHandler_ShouldFail_WhenReviewNotFound()
    {
        var handler = new UpdateReviewHandler(_mockUow.Object, _mockCache.Object);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Review)null);

        var res = await handler.HandleAsync(new UpdateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateReviewDto(4, "New T", "New C")));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateReviewHandler_ShouldSucceed()
    {
        var handler = new UpdateReviewHandler(_mockUow.Object, _mockCache.Object);
        var userId = Guid.NewGuid();
        var review = new Review { Id = Guid.NewGuid(), UserId = userId, Rating = 3, ParkingSpaceId = Guid.NewGuid() };
        _mockReviewRepo.Setup(r => r.GetByIdAsync(review.Id, It.IsAny<CancellationToken>())).ReturnsAsync(review);

        var res = await handler.HandleAsync(new UpdateReviewCommand(review.Id, userId, new UpdateReviewDto(5, "New T", "New C")));

        res.Success.Should().BeTrue();
        review.Rating.Should().Be(5);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // DeleteReviewHandler Tests
    [Fact]
    public async Task DeleteReviewHandler_ShouldSucceed()
    {
        var handler = new DeleteReviewHandler(_mockUow.Object, _mockCache.Object);
        var userId = Guid.NewGuid();
        var review = new Review { Id = Guid.NewGuid(), UserId = userId, ParkingSpaceId = Guid.NewGuid(), Rating = 4 };
        var parking = new ParkingSpace { Id = review.ParkingSpaceId, TotalReviews = 1, AverageRating = 4 };
        
        _mockReviewRepo.Setup(r => r.GetByIdAsync(review.Id, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(review.ParkingSpaceId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var res = await handler.HandleAsync(new DeleteReviewCommand(review.Id, userId));

        res.Success.Should().BeTrue();
        parking.TotalReviews.Should().Be(0);
        _mockReviewRepo.Verify(r => r.Remove(review), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // AddOwnerResponseHandler Tests
    [Fact]
    public async Task AddOwnerResponseHandler_ShouldSucceed()
    {
        var handler = new AddOwnerResponseHandler(_mockUow.Object, _mockCache.Object);
        var ownerId = Guid.NewGuid();
        var review = new Review { Id = Guid.NewGuid(), ParkingSpaceId = Guid.NewGuid() };
        var parking = new ParkingSpace { Id = review.ParkingSpaceId, OwnerId = ownerId };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(review.Id, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(review.ParkingSpaceId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var res = await handler.HandleAsync(new AddOwnerResponseCommand(review.Id, ownerId, new OwnerResponseDto("Thanks")));

        res.Success.Should().BeTrue();
        review.OwnerResponse.Should().Be("Thanks");
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
