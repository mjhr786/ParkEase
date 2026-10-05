using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Messaging.Contracts.Enums;

namespace ParkingApp.Marketplace.Application.EventHandlers;

internal sealed class BackgroundJobNotificationHandlers : 
    IDomainEventHandler<BookingSessionEndRemindedEvent>,
    IDomainEventHandler<BookingOverstayNotifiedEvent>,
    IDomainEventHandler<BookingOverstayFeeAssessedEvent>,
    IDomainEventHandler<BookingAutoCheckedOutEvent>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationSender _notificationSender;
    private readonly IOptionsMonitor<LprAccessOptions> _lprOptions;
    private readonly ILogger<BackgroundJobNotificationHandlers> _logger;

    public BackgroundJobNotificationHandlers(
        IMarketplaceUnitOfWork unitOfWork,
        INotificationSender notificationSender,
        IOptionsMonitor<LprAccessOptions> lprOptions,
        ILogger<BackgroundJobNotificationHandlers> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationSender = notificationSender;
        _lprOptions = lprOptions;
        _logger = logger;
    }

    private static bool CanRequestExtension(Booking booking) =>
        (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.InProgress)
        && !booking.HasPendingExtension
        && booking.Status != BookingStatus.PendingExtension
        && booking.Status != BookingStatus.AwaitingExtensionPayment;

    public async Task HandleAsync(BookingSessionEndRemindedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(domainEvent.BookingId, cancellationToken);
        if (booking == null) return;

        var title = booking.ParkingSpace?.Title ?? "parking";
        var reference = booking.BookingReference ?? booking.Id.ToString("N")[..8];
        var minutesLeft = Math.Max(1, (int)Math.Ceiling((booking.EndDateTime - DateTime.UtcNow).TotalMinutes));
        var canExtend = CanRequestExtension(booking);

        var msg = $"Your booking at {title} (ref {reference}) ends in about {minutesLeft} minute(s).";
        msg += canExtend ? " Tap Extend to stay longer, or Check out when you leave." : " Open your booking for details.";

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.SystemAlert.ToString(),
                canExtend ? $"Parking ends in ~{minutesLeft} min — extend?" : $"Parking ends in ~{minutesLeft} min",
                msg,
                Channels: new[] { "InApp" },
                Data: new Dictionary<string, string>
                {
                    { "BookingId", booking.Id.ToString() },
                    { "Type", "booking.session.ending" },
                    { "CanExtend", canExtend ? "true" : "false" },
                    { "ActionExtend", canExtend ? "true" : "false" },
                    { "ActionCheckout", "true" },
                    { "MinutesLeft", minutesLeft.ToString() },
                    { "CheckoutPath", $"/bookings/{booking.Id}" },
                    { "ExtendPath", canExtend ? $"/bookings/{booking.Id}?action=extend" : string.Empty }
                }),
            cancellationToken);
    }

    public async Task HandleAsync(BookingOverstayNotifiedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(domainEvent.BookingId, cancellationToken);
        if (booking == null) return;

        var title = booking.ParkingSpace?.Title ?? "parking";
        var reference = booking.BookingReference ?? booking.Id.ToString("N")[..8];
        var minutesLate = Math.Max(0, (int)(DateTime.UtcNow - booking.EndDateTime).TotalMinutes);
        var canExtend = CanRequestExtension(booking);
        var opts = _lprOptions.CurrentValue.Overstay;

        var msg = $"Your booking at {title} (ref {reference}) ended {minutesLate} min ago and is still active.";
        if (canExtend)
            msg += " Please open My Bookings to extend your stay (request more time) if you need to keep parking, or check out if you are leaving.";
        else
            msg += " An extension is not available right now (a request may already be pending). Please check out from My Bookings if you are leaving.";

        if (opts.FeesEnabled)
            msg += " Overstay fees may apply until you check out.";

        if (opts.AutoCheckOutEnabled)
        {
            var totalMins = Math.Clamp(opts.GraceMinutes, 0, 24 * 60) + Math.Clamp(opts.AutoCheckOutMinutes, 0, 7 * 24 * 60);
            msg += $" Auto check-out may occur after about {totalMins} minutes past the scheduled end.";
        }

        var data = new Dictionary<string, string>
        {
            { "BookingId", booking.Id.ToString() },
            { "Type", "booking.overstay" },
            { "CanExtend", canExtend ? "true" : "false" },
            { "ActionCheckout", "true" },
            { "ActionExtend", canExtend ? "true" : "false" },
            { "CheckoutPath", $"/bookings/{booking.Id}" },
            { "ExtendPath", canExtend ? $"/bookings/{booking.Id}?action=extend" : string.Empty }
        };

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.SystemAlert.ToString(),
                canExtend ? "Overstay — extend or check out" : "Overstay — please check out",
                msg,
                Channels: new[] { "InApp" },
                Data: data),
            cancellationToken);

        if (booking.ParkingSpace is { OwnerId: var ownerId } && ownerId != Guid.Empty && ownerId != booking.UserId)
        {
            await _notificationSender.SendAsync(
                ownerId,
                new NotificationSendRequest(
                    NotificationType.SystemAlert.ToString(),
                    "Guest overstay",
                    $"A guest is overstaying at {title} (ref {reference}, ~{minutesLate} min late). They were asked to extend or check out.",
                    Channels: new[] { "InApp" },
                    Data: new Dictionary<string, string>
                    {
                        { "BookingId", booking.Id.ToString() },
                        { "Type", "booking.overstay" },
                        { "Action", "open_booking" }
                    }),
                cancellationToken);
        }
    }

    public async Task HandleAsync(BookingOverstayFeeAssessedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(domainEvent.BookingId, cancellationToken);
        if (booking == null) return;

        var title = booking.ParkingSpace?.Title ?? "parking";
        var reference = booking.BookingReference ?? booking.Id.ToString("N")[..8];
        var canExtend = CanRequestExtension(booking);

        var feeMsg = canExtend
            ? $"An overstay fee of {domainEvent.FeeAmount:0.00} was added to booking {reference} (~{domainEvent.BillableMinutes} billable min at {title}). Extend your booking to stay longer, or check out to stop further fees."
            : $"An overstay fee of {domainEvent.FeeAmount:0.00} was added to booking {reference} (~{domainEvent.BillableMinutes} billable min at {title}). Please check out to stop further fees.";

        var data = new Dictionary<string, string>
        {
            { "BookingId", booking.Id.ToString() },
            { "Type", "booking.overstay.fee" },
            { "CanExtend", canExtend ? "true" : "false" },
            { "ActionCheckout", "true" },
            { "ActionExtend", canExtend ? "true" : "false" },
            { "CheckoutPath", $"/bookings/{booking.Id}" },
            { "ExtendPath", canExtend ? $"/bookings/{booking.Id}?action=extend" : string.Empty },
            { "FeeAmount", domainEvent.FeeAmount.ToString("0.00") }
        };

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.SystemAlert.ToString(),
                canExtend ? "Overstay fee — extend or check out" : "Overstay fee — please check out",
                feeMsg,
                Channels: new[] { "InApp" },
                Data: data),
            cancellationToken);
    }

    public async Task HandleAsync(BookingAutoCheckedOutEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(domainEvent.BookingId, cancellationToken);
        if (booking == null) return;

        var title = booking.ParkingSpace?.Title ?? "parking";
        var reference = booking.BookingReference ?? booking.Id.ToString("N")[..8];

        var feeNote = domainEvent.OverstayFeeAmount > 0
            ? $" Overstay fee on booking: {domainEvent.OverstayFeeAmount:0.00}."
            : string.Empty;

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.SystemAlert.ToString(),
                "Auto check-out",
                $"Your booking at {title} (ref {reference}) was automatically checked out after overstay.{feeNote}",
                Channels: new[] { "InApp" },
                Data: new Dictionary<string, string>
                {
                    { "BookingId", booking.Id.ToString() },
                    { "Type", "booking.overstay.autocheckout" }
                }),
            cancellationToken);

        if (booking.ParkingSpace is { OwnerId: var ownerId } && ownerId != Guid.Empty && ownerId != booking.UserId)
        {
            await _notificationSender.SendAsync(
                ownerId,
                new NotificationSendRequest(
                    NotificationType.SystemAlert.ToString(),
                    "Guest auto check-out",
                    $"Guest booking {reference} at {title} was auto-checked out after overstay.",
                    Channels: new[] { "InApp" },
                    Data: new Dictionary<string, string>
                    {
                        { "BookingId", booking.Id.ToString() },
                        { "Type", "booking.overstay.autocheckout" }
                    }),
                cancellationToken);
        }
    }
}
