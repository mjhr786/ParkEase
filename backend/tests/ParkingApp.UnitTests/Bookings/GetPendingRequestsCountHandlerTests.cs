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

public class GetPendingRequestsCountHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IBookingRepository> _mockBookingRepo;

    public GetPendingRequestsCountHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockBookingRepo = new Mock<IBookingRepository>();

        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        _mockUnitOfWork.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_WithNoParkingSpaces_ShouldReturnZero()
    {
        // Arrange
        var handler = new GetPendingRequestsCountHandler(_mockUnitOfWork.Object);
        var vendorId = Guid.NewGuid();
        var query = new GetPendingRequestsCountQuery(vendorId);

        _mockParkingRepo.Setup(r => r.GetByOwnerIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkingSpace>());

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WithMultipleSpaces_ShouldSumPendingBookings()
    {
        // Arrange
        var handler = new GetPendingRequestsCountHandler(_mockUnitOfWork.Object);
        var vendorId = Guid.NewGuid();
        var query = new GetPendingRequestsCountQuery(vendorId);

        var space1 = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = vendorId };
        var space2 = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = vendorId };
        var spaces = new List<ParkingSpace> { space1, space2 };

        _mockParkingRepo.Setup(r => r.GetByOwnerIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(spaces);

        var bookings1 = new List<Booking>
        {
            new Booking { Id = Guid.NewGuid(), Status = BookingStatus.Pending },
            new Booking { Id = Guid.NewGuid(), Status = BookingStatus.Confirmed }
        };
        var bookings2 = new List<Booking>
        {
            new Booking { Id = Guid.NewGuid(), Status = BookingStatus.Pending },
            new Booking { Id = Guid.NewGuid(), Status = BookingStatus.Pending }
        };

        _mockBookingRepo.Setup(r => r.GetByParkingSpaceIdAsync(space1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings1);
        _mockBookingRepo.Setup(r => r.GetByParkingSpaceIdAsync(space2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bookings2);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(3); // 1 from space1, 2 from space2
    }
}
