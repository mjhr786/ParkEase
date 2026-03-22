using Moq;
using FluentAssertions;
using Xunit;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Application.CQRS.Handlers.Bookings;

namespace ParkingApp.UnitTests.Bookings;

public class GetBookingsByParkingSpaceHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IBookingRepository> _mockBookingRepo;

    public GetBookingsByParkingSpaceHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockBookingRepo = new Mock<IBookingRepository>();

        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        _mockUnitOfWork.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenParkingSpaceNotFound_ShouldReturnUnauthorized()
    {
        // Arrange
        var handler = new GetBookingsByParkingSpaceHandler(_mockUnitOfWork.Object);
        var query = new GetBookingsByParkingSpaceQuery(Guid.NewGuid(), Guid.NewGuid(), null);

        _mockParkingRepo.Setup(r => r.GetByIdAsync(query.ParkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpace?)null);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotOwner_ShouldReturnUnauthorized()
    {
        // Arrange
        var handler = new GetBookingsByParkingSpaceHandler(_mockUnitOfWork.Object);
        var parkingSpace = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() }; // Different owner
        var query = new GetBookingsByParkingSpaceQuery(parkingSpace.Id, Guid.NewGuid(), null);

        _mockParkingRepo.Setup(r => r.GetByIdAsync(query.ParkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parkingSpace);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task HandleAsync_WithNoFilters_ShouldReturnAllBookingsMappedToDto()
    {
        // Arrange
        var handler = new GetBookingsByParkingSpaceHandler(_mockUnitOfWork.Object);
        var vendorId = Guid.NewGuid();
        var parkingSpace = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = vendorId };
        var query = new GetBookingsByParkingSpaceQuery(parkingSpace.Id, vendorId, null);

        var bookings = new List<Booking>
        {
            new Booking { Id = Guid.NewGuid(), Status = BookingStatus.Confirmed },
            new Booking { Id = Guid.NewGuid(), Status = BookingStatus.Pending }
        };

        _mockParkingRepo.Setup(r => r.GetByIdAsync(query.ParkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parkingSpace);
        _mockBookingRepo.Setup(r => r.GetByParkingSpaceIdAsync(query.ParkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Bookings.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(2);
    }
}
