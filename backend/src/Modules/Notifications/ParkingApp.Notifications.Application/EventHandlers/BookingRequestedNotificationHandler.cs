using Microsoft.Extensions.Logging;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using NotificationType = ParkingApp.Messaging.Contracts.Enums.NotificationType;
using ParkingApp.Messaging.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Events;

namespace ParkingApp.Notifications.Application.EventHandlers;

/// <summary>
/// In-app (and related) notifications for booking lifecycle ΓÇö outbox/event path, not command hot path.
/// </summary>
internal sealed class BookingRequestedNotificationHandler : IDomainEventHandler<BookingRequestedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly INotificationSender _notificationSender;
    private readonly ILogger<BookingRequestedNotificationHandler> _logger;

    public BookingRequestedNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        INotificationSender notificationSender,
        ILogger<BookingRequestedNotificationHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _notificationSender = notificationSender;
        _logger = logger;
    }

    public async Task HandleAsync(BookingRequestedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        if (parking is null || parking.OwnerId == Guid.Empty)
            return;

        var reference = domainEvent.BookingReference ?? domainEvent.BookingId.ToString("N")[..8];
        await _notificationSender.SendAsync(
            parking.OwnerId,
            new NotificationSendRequest(
                NotificationType.BookingRequest.ToString(),
                "New Booking Request",
                $"New booking request for {parking.Title} (ref {reference})",
                Channels: new[] { "InApp" },
                Data: new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty }
                }),
            cancellationToken);

        _logger.LogInformation(
            "Owner {OwnerId} notified of booking request {BookingId}",
            parking.OwnerId,
            domainEvent.BookingId);
    }
}

internal sealed class BookingApprovedNotificationHandler : IDomainEventHandler<BookingApprovedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly INotificationSender _notificationSender;

    public BookingApprovedNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        INotificationSender notificationSender)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _notificationSender = notificationSender;
    }

    public async Task HandleAsync(BookingApprovedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        var title = parking?.Title ?? "your parking space";

        var message = domainEvent.RequiresPayment
            ? $"Your booking for {title} has been approved. Please complete payment."
            : $"Your booking for {title} has been approved and is awaiting final settlement.";

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.BookingConfirmed.ToString(),
                "Booking Approved!",
                message,
                Channels: new[] { "InApp" },
                Data: new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty }
                }),
            cancellationToken);
    }
}

internal sealed class BookingConfirmedNotificationHandler : IDomainEventHandler<BookingConfirmedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly INotificationSender _notificationSender;

    public BookingConfirmedNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        INotificationSender notificationSender)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _notificationSender = notificationSender;
    }

    public async Task HandleAsync(BookingConfirmedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        var title = parking?.Title ?? "your parking space";

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.BookingConfirmed.ToString(),
                "Booking Confirmed!",
                $"Your booking for {title} has been approved and confirmed.",
                Channels: new[] { "InApp" },
                Data: new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty }
                }),
            cancellationToken);
    }
}

internal sealed class BookingRejectedNotificationHandler : IDomainEventHandler<BookingRejectedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly INotificationSender _notificationSender;
    private readonly IEmailService _email;
    private readonly ILogger<BookingRejectedNotificationHandler> _logger;

    public BookingRejectedNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        INotificationSender notificationSender,
        IEmailService email,
        ILogger<BookingRejectedNotificationHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _notificationSender = notificationSender;
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(BookingRejectedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        var title = parking?.Title ?? "your parking space";
        var reason = domainEvent.Reason ?? "Rejected by vendor";

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.BookingRejected.ToString(),
                "Booking Rejected",
                $"Your booking for {title} was rejected. Reason: {reason}",
                Channels: null,
                Data: new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                    { "Reason", reason }
                }),
            cancellationToken);

        if (domainEvent.VendorUserId is { } vendorId && vendorId != Guid.Empty)
        {
            await _notificationSender.SendAsync(
                vendorId,
                new NotificationSendRequest(
                    NotificationType.BookingRejected.ToString(),
                    "Booking Rejected",
                    $"You have rejected booking {domainEvent.BookingReference}",
                    Channels: null,
                    Data: new Dictionary<string, string>
                    {
                        { "BookingId", domainEvent.BookingId.ToString() },
                        { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                        { "Silent", "true" }
                    }),
                cancellationToken);
        }

        try
        {
            var member = await _userLookup.GetByIdAsync(domainEvent.UserId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(member?.Email))
            {
                await _email.SendEmailAsync(
                    member.Email,
                    $"Booking Rejected: {domainEvent.BookingReference}",
                    $"<p>Hello {member.FirstName},</p><p>We're sorry, but your booking for <strong>{title}</strong> was rejected.</p><p><strong>Reason:</strong> {reason}</p>");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BookingRejected email failed for booking {BookingId}", domainEvent.BookingId);
            throw;
        }
    }
}
