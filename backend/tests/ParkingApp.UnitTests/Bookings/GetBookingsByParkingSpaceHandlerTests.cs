using Moq;
using FluentAssertions;
using Xunit;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Application.CQRS.Handlers.Bookings;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.UnitTests.Bookings;

public class GetBookingsByParkingSpaceHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo = new();
    private readonly Mock<IBookingReadStore> _mockReadStore = new();

    public GetBookingsByParkingSpaceHandlerTests()
    {
        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenParkingSpaceNotFound_ShouldReturnUnauthorized()
    {
        var handler = new GetBookingsByParkingSpaceHandler(_mockUnitOfWork.Object, _mockReadStore.Object);
        var query = new GetBookingsByParkingSpaceQuery(Guid.NewGuid(), Guid.NewGuid(), null);

        _mockParkingRepo.Setup(r => r.GetByIdAsync(query.ParkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpace?)null);

        var result = await handler.HandleAsync(query);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotOwner_ShouldReturnUnauthorized()
    {
        var handler = new GetBookingsByParkingSpaceHandler(_mockUnitOfWork.Object, _mockReadStore.Object);
        var parkingSpace = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = Guid.NewGuid() };
        var query = new GetBookingsByParkingSpaceQuery(parkingSpace.Id, Guid.NewGuid(), null);

        _mockParkingRepo.Setup(r => r.GetByIdAsync(query.ParkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parkingSpace);

        var result = await handler.HandleAsync(query);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task HandleAsync_WithNoFilters_ShouldReturnListFromReadStore()
    {
        var handler = new GetBookingsByParkingSpaceHandler(_mockUnitOfWork.Object, _mockReadStore.Object);
        var vendorId = Guid.NewGuid();
        var parkingSpace = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = vendorId };
        var query = new GetBookingsByParkingSpaceQuery(parkingSpace.Id, vendorId, null);

        _mockParkingRepo.Setup(r => r.GetByIdAsync(query.ParkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parkingSpace);

        var list = new BookingListResultDto(
            new List<BookingDto>
            {
                new() { Id = Guid.NewGuid(), Status = BookingStatus.Confirmed },
                new() { Id = Guid.NewGuid(), Status = BookingStatus.Pending }
            },
            2, 1, 20, 1);

        _mockReadStore.Setup(r => r.GetByParkingSpaceAsync(parkingSpace.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var result = await handler.HandleAsync(query);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Bookings.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(2);
    }
}
