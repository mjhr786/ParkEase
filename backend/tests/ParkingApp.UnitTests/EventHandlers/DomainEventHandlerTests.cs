using ParkingApp.Application.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.Caching;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Marketplace.Application.EventHandlers;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Marketplace.Domain.Entities;
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
        var parkingLookup = new Mock<IParkingSpaceLookup>();
        var parkingId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();

        parkingLookup.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpaceSummary(parkingId, vendorId, "Lot", true, 5, "IndividualVendor"));

        var handler = new BookingCancelledParkingCacheHandler(cache.Object, parkingLookup.Object);

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
    public async Task BookingCheckedInNotificationHandler_NotifiesOwner()
    {
        var parkingLookup = new Mock<IParkingSpaceLookup>();
        var userLookup = new Mock<IUserLookup>();
        var notifications = new Mock<INotificationSender>();
        var logger = new Mock<ILogger<BookingCheckedInNotificationHandler>>();

        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        parkingLookup.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpaceSummary(parkingId, ownerId, "Downtown Lot", true, 5, "IndividualVendor"));
        userLookup.Setup(r => r.GetByIdAsync(memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(memberId, "a@b.com", "Ada", "Lovelace"));

        var handler = new BookingCheckedInNotificationHandler(
            parkingLookup.Object, userLookup.Object, notifications.Object, logger.Object);
        await handler.HandleAsync(new BookingCheckedInEvent(Guid.NewGuid(), memberId, parkingId, "REF-CI"));

        notifications.Verify(n => n.SendAsync(
            ownerId,
            It.Is<NotificationSendRequest>(r =>
                r.Title == "Guest Checked In" && r.Message.Contains("Ada") && r.Message.Contains("Downtown Lot")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookingCancelledNotificationHandler_NotifiesOwner()
    {
        var parkingLookup = new Mock<IParkingSpaceLookup>();
        var notifications = new Mock<INotificationSender>();
        var logger = new Mock<ILogger<BookingCancelledNotificationHandler>>();

        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        parkingLookup.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpaceSummary(parkingId, ownerId, "Lot", true, 5, "IndividualVendor"));

        var handler = new BookingCancelledNotificationHandler(parkingLookup.Object, notifications.Object, logger.Object);
        await handler.HandleAsync(new BookingCancelledEvent(Guid.NewGuid(), memberId, parkingId, "REF9", "changed plans"));

        notifications.Verify(n => n.SendAsync(
            ownerId,
            It.Is<NotificationSendRequest>(r => r.Title == "Booking Cancelled" && r.Message.Contains("REF9")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookingCancelledNotificationHandler_SkipsWhenCancellerIsOwner()
    {
        var parkingLookup = new Mock<IParkingSpaceLookup>();
        var notifications = new Mock<INotificationSender>();
        var logger = new Mock<ILogger<BookingCancelledNotificationHandler>>();

        var ownerId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        parkingLookup.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpaceSummary(parkingId, ownerId, "Lot", true, 5, "IndividualVendor"));

        var handler = new BookingCancelledNotificationHandler(parkingLookup.Object, notifications.Object, logger.Object);
        await handler.HandleAsync(new BookingCancelledEvent(Guid.NewGuid(), ownerId, parkingId, "REF9", "self"));

        notifications.Verify(n => n.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationSendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Booking_CheckOut_RaisesCheckedOutEvent()
    {
        var booking = new Booking { Status = BookingStatus.InProgress, UserId = Guid.NewGuid(), ParkingSpaceId = Guid.NewGuid() };
        booking.CheckOut();
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCheckedOutEvent);
    }
}






