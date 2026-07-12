using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Events.Bookings;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.EventHandlers;

internal static class BookingCacheInvalidation
{
    public static async Task InvalidateAsync(ICacheService cache, Guid parkingSpaceId, CancellationToken cancellationToken)
    {
        if (parkingSpaceId == Guid.Empty)
            return;

        await cache.RemoveAsync($"parking:{parkingSpaceId}", cancellationToken);
        await cache.RemoveByPatternAsync("search:*", cancellationToken);
    }
}

public sealed class BookingConfirmedParkingCacheHandler : IDomainEventHandler<BookingConfirmedEvent>
{
    private readonly ICacheService _cache;

    public BookingConfirmedParkingCacheHandler(ICacheService cache) => _cache = cache;

    public Task HandleAsync(BookingConfirmedEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(_cache, domainEvent.ParkingSpaceId, cancellationToken);
}

public sealed class BookingCancelledParkingCacheHandler : IDomainEventHandler<BookingCancelledEvent>
{
    private readonly ICacheService _cache;

    public BookingCancelledParkingCacheHandler(ICacheService cache) => _cache = cache;

    public Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(_cache, domainEvent.ParkingSpaceId, cancellationToken);
}

public sealed class BookingCheckedInParkingCacheHandler : IDomainEventHandler<BookingCheckedInEvent>
{
    private readonly ICacheService _cache;

    public BookingCheckedInParkingCacheHandler(ICacheService cache) => _cache = cache;

    public Task HandleAsync(BookingCheckedInEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(_cache, domainEvent.ParkingSpaceId, cancellationToken);
}

public sealed class BookingCheckedOutParkingCacheHandler : IDomainEventHandler<BookingCheckedOutEvent>
{
    private readonly ICacheService _cache;

    public BookingCheckedOutParkingCacheHandler(ICacheService cache) => _cache = cache;

    public Task HandleAsync(BookingCheckedOutEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(_cache, domainEvent.ParkingSpaceId, cancellationToken);
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
