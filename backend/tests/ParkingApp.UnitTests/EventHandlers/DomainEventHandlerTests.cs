using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.Caching;
using ParkingApp.Application.EventHandlers;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Events.Bookings;
using ParkingApp.Domain.Events.Parking;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.EventHandlers;

public class DomainEventHandlerTests
{
    [Fact]
    public async Task ParkingSpaceUpdatedCacheHandler_InvalidatesDetailSearchAndMap()
    {
        var cache = new Mock<ICacheService>();
        var logger = new Mock<ILogger<ParkingSpaceUpdatedCacheHandler>>();
        var handler = new ParkingSpaceUpdatedCacheHandler(cache.Object, logger.Object);
        var parkingId = Guid.NewGuid();

        await handler.HandleAsync(new ParkingSpaceUpdatedEvent(parkingId, "Lot A"));

        cache.Verify(c => c.RemoveAsync(CacheKeys.Parking(parkingId), It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.SearchAll, It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.MapAll, It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.ParkingForecastAll, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookingCancelledParkingCacheHandler_InvalidatesParkingCachesComprehensively()
    {
        var cache = new Mock<ICacheService>();
        var uow = new Mock<IMarketplaceUnitOfWork>();
        var parkingRepo = new Mock<IParkingSpaceRepository>();
        var parkingId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();

        parkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, OwnerId = vendorId });
        uow.Setup(u => u.ParkingSpaces).Returns(parkingRepo.Object);

        var handler = new BookingCancelledParkingCacheHandler(cache.Object, uow.Object);

        await handler.HandleAsync(new BookingCancelledEvent(Guid.NewGuid(), memberId, parkingId, "REF1", "reason"));

        cache.Verify(c => c.RemoveAsync(CacheKeys.Parking(parkingId), It.IsAny<CancellationToken>()), Times.Once);
        // Discovery lists are intentionally not busted on booking lifecycle (stable listing metadata).
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.SearchAll, It.IsAny<CancellationToken>()), Times.Never);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.MapAll, It.IsAny<CancellationToken>()), Times.Never);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.ParkingForecastAll, It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(CacheKeys.MemberDashboard(memberId), It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(CacheKeys.VendorDashboard(vendorId), It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveByPatternAsync(CacheKeys.OwnerForecastAll, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookingCancelledNotificationHandler_NotifiesOwner()
    {
        var uow = new Mock<IUnitOfWork>();
        var parkingRepo = new Mock<IParkingSpaceRepository>();
        var notifications = new Mock<INotificationCoordinator>();
        var logger = new Mock<ILogger<BookingCancelledNotificationHandler>>();

        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        parkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, OwnerId = ownerId });
        uow.Setup(u => u.ParkingSpaces).Returns(parkingRepo.Object);

        var handler = new BookingCancelledNotificationHandler(uow.Object, notifications.Object, logger.Object);
        await handler.HandleAsync(new BookingCancelledEvent(Guid.NewGuid(), memberId, parkingId, "REF9", "changed plans"));

        notifications.Verify(n => n.SendAsync(
            ownerId,
            It.Is<NotificationRequest>(r => r.Title == "Booking Cancelled" && r.Message.Contains("REF9")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookingCancelledNotificationHandler_SkipsWhenCancellerIsOwner()
    {
        var uow = new Mock<IUnitOfWork>();
        var parkingRepo = new Mock<IParkingSpaceRepository>();
        var notifications = new Mock<INotificationCoordinator>();
        var logger = new Mock<ILogger<BookingCancelledNotificationHandler>>();

        var ownerId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        parkingRepo.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, OwnerId = ownerId });
        uow.Setup(u => u.ParkingSpaces).Returns(parkingRepo.Object);

        var handler = new BookingCancelledNotificationHandler(uow.Object, notifications.Object, logger.Object);
        await handler.HandleAsync(new BookingCancelledEvent(Guid.NewGuid(), ownerId, parkingId, "REF9", "self"));

        notifications.Verify(n => n.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Booking_CheckOut_RaisesCheckedOutEvent()
    {
        var booking = new Booking { Status = BookingStatus.InProgress, UserId = Guid.NewGuid(), ParkingSpaceId = Guid.NewGuid() };
        booking.CheckOut();
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCheckedOutEvent);
    }
}
