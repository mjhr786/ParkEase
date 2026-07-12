using Microsoft.Extensions.Logging;
using ParkingApp.Application.Caching;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Events.Bookings;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.EventHandlers;

/// <summary>
/// Shared accurate invalidation for booking lifecycle events.
/// Resolves parking owner so vendor dashboards and owner forecasts stay correct.
/// </summary>
internal static class BookingCacheInvalidation
{
    public static async Task InvalidateAsync(
        ICacheService cache,
        IMarketplaceUnitOfWork unitOfWork,
        Guid parkingSpaceId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        if (parkingSpaceId == Guid.Empty)
            return;

        Guid? vendorId = null;
        try
        {
            var parking = await unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
            vendorId = parking?.OwnerId;
        }
        catch
        {
            // Still invalidate parking/discovery/forecasts even if owner lookup fails.
        }

        await CacheInvalidation.ForBookingChangeAsync(
            cache,
            parkingSpaceId,
            memberId: memberId,
            vendorId: vendorId,
            cancellationToken);
    }
}

public sealed class BookingConfirmedParkingCacheHandler : IDomainEventHandler<BookingConfirmedEvent>
{
    private readonly ICacheService _cache;
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public BookingConfirmedParkingCacheHandler(ICacheService cache, IMarketplaceUnitOfWork unitOfWork)
    {
        _cache = cache;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(BookingConfirmedEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(
            _cache, _unitOfWork, domainEvent.ParkingSpaceId, domainEvent.UserId, cancellationToken);
}

public sealed class BookingCancelledParkingCacheHandler : IDomainEventHandler<BookingCancelledEvent>
{
    private readonly ICacheService _cache;
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public BookingCancelledParkingCacheHandler(ICacheService cache, IMarketplaceUnitOfWork unitOfWork)
    {
        _cache = cache;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(
            _cache, _unitOfWork, domainEvent.ParkingSpaceId, domainEvent.UserId, cancellationToken);
}

public sealed class BookingCheckedInParkingCacheHandler : IDomainEventHandler<BookingCheckedInEvent>
{
    private readonly ICacheService _cache;
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public BookingCheckedInParkingCacheHandler(ICacheService cache, IMarketplaceUnitOfWork unitOfWork)
    {
        _cache = cache;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(BookingCheckedInEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(
            _cache, _unitOfWork, domainEvent.ParkingSpaceId, domainEvent.UserId, cancellationToken);
}

public sealed class BookingCheckedOutParkingCacheHandler : IDomainEventHandler<BookingCheckedOutEvent>
{
    private readonly ICacheService _cache;
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public BookingCheckedOutParkingCacheHandler(ICacheService cache, IMarketplaceUnitOfWork unitOfWork)
    {
        _cache = cache;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(BookingCheckedOutEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(
            _cache, _unitOfWork, domainEvent.ParkingSpaceId, domainEvent.UserId, cancellationToken);
}

/// <summary>
/// Notifies the parking owner when a member cancels a booking.
/// Runs after SaveChanges via the domain event dispatcher.
/// </summary>
public sealed class BookingCancelledNotificationHandler : IDomainEventHandler<BookingCancelledEvent>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly ILogger<BookingCancelledNotificationHandler> _logger;

    public BookingCancelledNotificationHandler(
        IMarketplaceUnitOfWork unitOfWork,
        INotificationCoordinator notificationCoordinator,
        ILogger<BookingCancelledNotificationHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _logger = logger;
    }

    public async Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        if (parking == null)
        {
            _logger.LogWarning(
                "BookingCancelledEvent for booking {BookingId}: parking {ParkingSpaceId} not found; skip owner notify",
                domainEvent.BookingId,
                domainEvent.ParkingSpaceId);
            return;
        }

        // Owner should hear about member cancellations (not self-cancel noise).
        if (parking.OwnerId == domainEvent.UserId)
            return;

        await _notificationCoordinator.SendAsync(
            parking.OwnerId,
            new NotificationRequest(
                NotificationType.BookingRejected.ToString(),
                "Booking Cancelled",
                $"Booking {domainEvent.BookingReference} has been cancelled",
                NotificationChannels.All,
                new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty }
                }),
            cancellationToken);

        _logger.LogInformation(
            "Owner {OwnerId} notified of cancelled booking {BookingId}",
            parking.OwnerId,
            domainEvent.BookingId);
    }
}
