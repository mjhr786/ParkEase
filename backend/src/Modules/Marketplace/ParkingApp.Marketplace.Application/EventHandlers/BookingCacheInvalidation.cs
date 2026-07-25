using Microsoft.Extensions.Logging;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Application.Caching;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Application.Interfaces;
using NotificationType = ParkingApp.Messaging.Contracts.Enums.NotificationType;

using ParkingApp.Messaging.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Events;

namespace ParkingApp.Marketplace.Application.EventHandlers;

/// <summary>
/// Shared accurate invalidation for booking lifecycle events.
/// Resolves parking owner so vendor dashboards and owner forecasts stay correct.
/// </summary>
internal static class BookingCacheInvalidation
{
    public static async Task InvalidateAsync(
        ICacheService cache,
        IParkingSpaceLookup parkingSpaceLookup,
        Guid parkingSpaceId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        if (parkingSpaceId == Guid.Empty)
            return;

        Guid? vendorId = null;
        try
        {
            var parking = await parkingSpaceLookup.GetByIdAsync(parkingSpaceId, cancellationToken);
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

internal sealed class BookingConfirmedParkingCacheHandler : IDomainEventHandler<BookingConfirmedEvent>
{
    private readonly ICacheService _cache;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;

    public BookingConfirmedParkingCacheHandler(ICacheService cache, IParkingSpaceLookup parkingSpaceLookup)
    {
        _cache = cache;
        _parkingSpaceLookup = parkingSpaceLookup;
    }

    public Task HandleAsync(BookingConfirmedEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(
            _cache, _parkingSpaceLookup, domainEvent.ParkingSpaceId, domainEvent.UserId, cancellationToken);
}

internal sealed class BookingCancelledParkingCacheHandler : IDomainEventHandler<BookingCancelledEvent>
{
    private readonly ICacheService _cache;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;

    public BookingCancelledParkingCacheHandler(ICacheService cache, IParkingSpaceLookup parkingSpaceLookup)
    {
        _cache = cache;
        _parkingSpaceLookup = parkingSpaceLookup;
    }

    public Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(
            _cache, _parkingSpaceLookup, domainEvent.ParkingSpaceId, domainEvent.UserId, cancellationToken);
}

internal sealed class BookingApprovedParkingCacheHandler : IDomainEventHandler<BookingApprovedEvent>
{
    private readonly ICacheService _cache;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;

    public BookingApprovedParkingCacheHandler(ICacheService cache, IParkingSpaceLookup parkingSpaceLookup)
    {
        _cache = cache;
        _parkingSpaceLookup = parkingSpaceLookup;
    }

    public Task HandleAsync(BookingApprovedEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(
            _cache, _parkingSpaceLookup, domainEvent.ParkingSpaceId, domainEvent.UserId, cancellationToken);
}

internal sealed class BookingRejectedParkingCacheHandler : IDomainEventHandler<BookingRejectedEvent>
{
    private readonly ICacheService _cache;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;

    public BookingRejectedParkingCacheHandler(ICacheService cache, IParkingSpaceLookup parkingSpaceLookup)
    {
        _cache = cache;
        _parkingSpaceLookup = parkingSpaceLookup;
    }

    public Task HandleAsync(BookingRejectedEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(
            _cache, _parkingSpaceLookup, domainEvent.ParkingSpaceId, domainEvent.UserId, cancellationToken);
}

internal sealed class BookingCheckedInParkingCacheHandler : IDomainEventHandler<BookingCheckedInEvent>
{
    private readonly ICacheService _cache;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;

    public BookingCheckedInParkingCacheHandler(ICacheService cache, IParkingSpaceLookup parkingSpaceLookup)
    {
        _cache = cache;
        _parkingSpaceLookup = parkingSpaceLookup;
    }

    public Task HandleAsync(BookingCheckedInEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(
            _cache, _parkingSpaceLookup, domainEvent.ParkingSpaceId, domainEvent.UserId, cancellationToken);
}

internal sealed class BookingCheckedOutParkingCacheHandler : IDomainEventHandler<BookingCheckedOutEvent>
{
    private readonly ICacheService _cache;
    private readonly IParkingSpaceLookup _parkingSpaceLookup;

    public BookingCheckedOutParkingCacheHandler(ICacheService cache, IParkingSpaceLookup parkingSpaceLookup)
    {
        _cache = cache;
        _parkingSpaceLookup = parkingSpaceLookup;
    }

    public Task HandleAsync(BookingCheckedOutEvent domainEvent, CancellationToken cancellationToken = default) =>
        BookingCacheInvalidation.InvalidateAsync(
            _cache, _parkingSpaceLookup, domainEvent.ParkingSpaceId, domainEvent.UserId, cancellationToken);
}

/// <summary>
/// Notifies the parking owner when a guest checks in.
/// Side effect moved off the CheckIn hot path (via outbox / domain event dispatch).
/// </summary>
internal sealed class BookingCheckedInNotificationHandler : IDomainEventHandler<BookingCheckedInEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly INotificationSender _notificationSender;
    private readonly ILogger<BookingCheckedInNotificationHandler> _logger;

    public BookingCheckedInNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        INotificationSender notificationSender,
        ILogger<BookingCheckedInNotificationHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _notificationSender = notificationSender;
        _logger = logger;
    }

    public async Task HandleAsync(BookingCheckedInEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        if (parking is null || parking.OwnerId == Guid.Empty)
        {
            _logger.LogWarning(
                "BookingCheckedInEvent for booking {BookingId}: parking {ParkingSpaceId} not found; skip owner notify",
                domainEvent.BookingId,
                domainEvent.ParkingSpaceId);
            return;
        }

        var member = await _userLookup.GetByIdAsync(domainEvent.UserId, cancellationToken);
        var memberName = string.IsNullOrWhiteSpace(member?.FirstName) ? "A guest" : member.FirstName;

        await _notificationSender.SendAsync(
            parking.OwnerId,
            new NotificationSendRequest(
                NotificationType.SystemAlert.ToString(),
                "Guest Checked In",
                $"{memberName} has checked in at {parking.Title}",
                Channels: null,
                Data: new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty }
                }),
            cancellationToken);

        _logger.LogInformation(
            "Owner {OwnerId} notified of check-in for booking {BookingId}",
            parking.OwnerId,
            domainEvent.BookingId);
    }
}

/// <summary>
/// Notifies the parking owner when a member cancels a booking.
/// Runs after SaveChanges via the domain event dispatcher.
/// </summary>
internal sealed class BookingCancelledNotificationHandler : IDomainEventHandler<BookingCancelledEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly INotificationSender _notificationSender;
    private readonly ILogger<BookingCancelledNotificationHandler> _logger;

    public BookingCancelledNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        INotificationSender notificationSender,
        ILogger<BookingCancelledNotificationHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _notificationSender = notificationSender;
        _logger = logger;
    }

    public async Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
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

        await _notificationSender.SendAsync(
            parking.OwnerId,
            new NotificationSendRequest(
                NotificationType.BookingRejected.ToString(),
                "Booking Cancelled",
                $"Booking {domainEvent.BookingReference} has been cancelled",
                Channels: null,
                Data: new Dictionary<string, string>
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
