using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS.Commands.Reviews;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests;

public class ReviewTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IReviewRepository> _mockReviewRepository;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepository;
    private readonly Mock<IBookingRepository> _mockBookingRepository;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<CreateReviewHandler>> _mockCreateLogger;

    public ReviewTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockReviewRepository = new Mock<IReviewRepository>();
        _mockParkingRepository = new Mock<IParkingSpaceRepository>();
        _mockBookingRepository = new Mock<IBookingRepository>();
        _mockCache = new Mock<ICacheService>();
        _mockCreateLogger = new Mock<ILogger<CreateReviewHandler>>();

        _mockUnitOfWork.Setup(u => u.Reviews).Returns(_mockReviewRepository.Object);
        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepository.Object);
        _mockUnitOfWork.Setup(u => u.Bookings).Returns(_mockBookingRepository.Object);
    }

    [Fact]
    public async Task CreateReviewHandler_WhenParkingNotFound_ShouldReturnFailure()
    {
        var handler = new CreateReviewHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockCreateLogger.Object);
        _mockParkingRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace?)null);

        var dto = new CreateReviewDto(Guid.NewGuid(), null, 5, "Great", "Nice place");
        var result = await handler.HandleAsync(new CreateReviewCommand(Guid.NewGuid(), dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
    }

    [Fact]
    public async Task CreateReviewHandler_WithInvalidBooking_ShouldReturnFailure()
    {
        var handler = new CreateReviewHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockCreateLogger.Object);
        var parkingId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockParkingRepository.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(new ParkingSpace());
        _mockBookingRepository.Setup(r => r.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Booking { UserId = Guid.NewGuid(), Status = BookingStatus.Confirmed }); // Different user and wrong status

        var dto = new CreateReviewDto(parkingId, bookingId, 5, "Great", "Nice place");
        var result = await handler.HandleAsync(new CreateReviewCommand(userId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid booking reference");
    }

    [Fact]
    public async Task CreateReviewHandler_WhenAlreadyReviewed_ShouldReturnFailure()
    {
        var handler = new CreateReviewHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockCreateLogger.Object);
        var parkingId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockParkingRepository.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(new ParkingSpace());
        _mockReviewRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Review, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Review());

        var dto = new CreateReviewDto(parkingId, null, 5, "Great", "Nice place");
        var result = await handler.HandleAsync(new CreateReviewCommand(userId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You have already reviewed this parking space");
    }

    [Fact]
    public async Task CreateReviewHandler_WhenValid_ShouldCalculateNewRatingAndSucceed()
    {
        var handler = new CreateReviewHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockCreateLogger.Object);
        var parkingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, AverageRating = 4.0, TotalReviews = 1 };

        _mockParkingRepository.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockReviewRepository.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Review, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);
        _mockReviewRepository.Setup(r => r.GetAverageRatingAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(4.0);

        var dto = new CreateReviewDto(parkingId, null, 5, "Amazing", "Best ever");
        var result = await handler.HandleAsync(new CreateReviewCommand(userId, dto));

        result.Success.Should().BeTrue();
        parking.TotalReviews.Should().Be(2);
        parking.AverageRating.Should().Be(4.5); // (4.0 * 1 + 5) / 2
        _mockReviewRepository.Verify(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddOwnerResponse_WhenUnauthorized_ShouldReturnFailure()
    {
        var handler = new AddOwnerResponseHandler(_mockUnitOfWork.Object, _mockCache.Object);
        var reviewId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        
        var review = new Review { Id = reviewId, ParkingSpaceId = Guid.NewGuid() };
        var parking = new ParkingSpace { Id = review.ParkingSpaceId, OwnerId = otherOwnerId };

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockParkingRepository.Setup(r => r.GetByIdAsync(review.ParkingSpaceId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var dto = new OwnerResponseDto("Thank you!");
        var result = await handler.HandleAsync(new AddOwnerResponseCommand(reviewId, ownerId, dto));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }
}
