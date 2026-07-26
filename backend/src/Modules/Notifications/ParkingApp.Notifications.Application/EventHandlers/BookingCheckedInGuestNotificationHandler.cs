using Microsoft.Extensions.Logging;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Domain.Events;
using NotificationType = ParkingApp.Messaging.Contracts.Enums.NotificationType;

namespace ParkingApp.Notifications.Application.EventHandlers;

/// <summary>
/// Notifies the booking guest when check-in completes (manual or LPR).
/// </summary>
internal sealed class BookingCheckedInGuestNotificationHandler : IDomainEventHandler<BookingCheckedInEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly INotificationSender _notificationSender;
    private readonly ILogger<BookingCheckedInGuestNotificationHandler> _logger;

    public BookingCheckedInGuestNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        INotificationSender notificationSender,
        ILogger<BookingCheckedInGuestNotificationHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _notificationSender = notificationSender;
        _logger = logger;
    }

    public async Task HandleAsync(BookingCheckedInEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        var title = parking?.Title ?? "your parking space";
        var reference = domainEvent.BookingReference ?? domainEvent.BookingId.ToString("N")[..8];

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.SystemAlert.ToString(),
                "Check-in complete",
                $"Welcome to {title}! Your check-in is complete (ref {reference}).",
                Channels: new[] { "InApp" },
                Data: new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                    { "Type", "booking.checkin" }
                }),
            cancellationToken);

        _logger.LogInformation(
            "Guest {UserId} notified of check-in for booking {BookingId}",
            domainEvent.UserId,
            domainEvent.BookingId);
    }
}

/// <summary>
/// Notifies the booking guest when check-out completes (manual or LPR).
/// </summary>
internal sealed class BookingCheckedOutGuestNotificationHandler : IDomainEventHandler<BookingCheckedOutEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly INotificationSender _notificationSender;
    private readonly ILogger<BookingCheckedOutGuestNotificationHandler> _logger;

    public BookingCheckedOutGuestNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        INotificationSender notificationSender,
        ILogger<BookingCheckedOutGuestNotificationHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _notificationSender = notificationSender;
        _logger = logger;
    }

    public async Task HandleAsync(BookingCheckedOutEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        var title = parking?.Title ?? "your parking space";
        var reference = domainEvent.BookingReference ?? domainEvent.BookingId.ToString("N")[..8];

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.SystemAlert.ToString(),
                "Check-out complete",
                $"Check-out complete at {title}. Thanks for parking with ParkEase (ref {reference}).",
                Channels: new[] { "InApp" },
                Data: new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                    { "Type", "booking.checkout" }
                }),
            cancellationToken);

        _logger.LogInformation(
            "Guest {UserId} notified of check-out for booking {BookingId}",
            domainEvent.UserId,
            domainEvent.BookingId);
    }
}
