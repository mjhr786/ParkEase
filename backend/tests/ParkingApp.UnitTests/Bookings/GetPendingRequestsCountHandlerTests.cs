using Moq;
using FluentAssertions;
using Xunit;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.UnitTests.Bookings;

public class GetPendingRequestsCountHandlerTests
{
    private readonly Mock<IBookingReadStore> _mockReadStore = new();

    [Fact]
    public async Task HandleAsync_WithNoPending_ShouldReturnZero()
    {
        var vendorId = Guid.NewGuid();
        _mockReadStore.Setup(r => r.CountPendingForVendorAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new GetPendingRequestsCountHandler(_mockReadStore.Object, new Mock<ParkingApp.Application.Interfaces.ICacheService>().Object);
        var result = await handler.HandleAsync(new GetPendingRequestsCountQuery(vendorId));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_WithPending_ShouldReturnCountFromReadStore()
    {
        var vendorId = Guid.NewGuid();
        _mockReadStore.Setup(r => r.CountPendingForVendorAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var handler = new GetPendingRequestsCountHandler(_mockReadStore.Object, new Mock<ParkingApp.Application.Interfaces.ICacheService>().Object);
        var result = await handler.HandleAsync(new GetPendingRequestsCountQuery(vendorId));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(3);
    }
}
