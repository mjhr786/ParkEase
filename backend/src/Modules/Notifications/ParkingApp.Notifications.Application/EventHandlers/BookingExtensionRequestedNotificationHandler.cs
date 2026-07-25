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

internal sealed class BookingExtensionRequestedNotificationHandler : IDomainEventHandler<BookingExtensionRequestedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly INotificationSender _notificationSender;
    private readonly IEmailService _email;
    private readonly ILogger<BookingExtensionRequestedNotificationHandler> _logger;

    public BookingExtensionRequestedNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        INotificationSender notificationSender,
        IEmailService email,
        ILogger<BookingExtensionRequestedNotificationHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _notificationSender = notificationSender;
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(BookingExtensionRequestedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        if (parking is null)
            return;

        var member = await _userLookup.GetByIdAsync(domainEvent.UserId, cancellationToken);
        var memberName = string.IsNullOrWhiteSpace(member?.FullName) ? "A member" : member.FullName;
        var owner = parking.OwnerId != Guid.Empty
            ? await _userLookup.GetByIdAsync(parking.OwnerId, cancellationToken)
            : null;

        await _notificationSender.SendAsync(
            parking.OwnerId,
            new NotificationSendRequest(
                NotificationType.BookingRequest.ToString(),
                "Extension Request",
                $"{memberName} has requested an extension for booking {domainEvent.BookingReference} at {parking.Title}",
                Channels: new[] { "InApp" },
                Data: new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                    { "Type", "Extension" }
                }),
            cancellationToken);

        if (owner != null && !string.IsNullOrWhiteSpace(owner.Email))
        {
            try
            {
                await _email.SendEmailAsync(
                    owner.Email,
                    $"Extension Request: {domainEvent.BookingReference}",
                    $"<p>Hello {owner.FirstName},</p>" +
                    $"<p>{memberName} has requested to extend booking {domainEvent.BookingReference} at {parking.Title}.</p>" +
                    $"<p>Requested new end time: <strong>{domainEvent.NewEndUtc:f}</strong>.</p>" +
                    $"<p>Please log in to approve or reject this request.</p>");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Extension requested email failed for booking {BookingId}", domainEvent.BookingId);
            }
        }

        if (!string.IsNullOrWhiteSpace(member?.Email))
        {
            try
            {
                var requiresPayment = domainEvent.ExtraAmount > 0;
                await _email.SendEmailAsync(
                    member.Email,
                    $"Extension Requested: {domainEvent.BookingReference}",
                    $"<p>Hello {member.FirstName},</p>" +
                    $"<p>Your extension request for <strong>{parking.Title}</strong> has been sent to the owner.</p>" +
                    (requiresPayment
                        ? $"<p>If approved, an additional charge of <strong>INR {domainEvent.ExtraAmount:F2}</strong> will be due.</p>"
                        : "<p>If approved, your active parking pass pricing means no additional payment will be required.</p>") +
                    "<p>You will be notified once the owner responds.</p>");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Extension requested email failed for booking {BookingId}", domainEvent.BookingId);
            }
        }
    }
}

internal sealed class BookingExtensionApprovedNotificationHandler : IDomainEventHandler<BookingExtensionApprovedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly INotificationSender _notificationSender;
    private readonly IEmailService _email;

    public BookingExtensionApprovedNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        INotificationSender notificationSender,
        IEmailService email)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _notificationSender = notificationSender;
        _email = email;
    }

    public async Task HandleAsync(BookingExtensionApprovedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        var title = parking?.Title ?? "your parking space";

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.BookingConfirmed.ToString(),
                domainEvent.RequiresPayment ? "Extension Approved!" : "Extension Confirmed!",
                domainEvent.RequiresPayment
                    ? $"Your extension request for {title} was approved. Please complete the payment."
                    : $"Your extension request for {title} was approved and confirmed with your parking pass pricing.",
                Channels: new[] { "InApp" },
                Data: new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                    { "Type", "Extension" }
                }),
            cancellationToken);

        if (domainEvent.VendorUserId is { } vendorId && vendorId != Guid.Empty)
        {
            await _notificationSender.SendAsync(
                vendorId,
                new NotificationSendRequest(
                    NotificationType.BookingConfirmed.ToString(),
                    "Extension Approved",
                    $"You have approved the extension for {domainEvent.BookingReference}",
                    Channels: new[] { "InApp" },
                    Data: new Dictionary<string, string>
                    {
                        { "BookingId", domainEvent.BookingId.ToString() },
                        { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                        { "Type", "Extension" },
                        { "Silent", "true" }
                    }),
                cancellationToken);
        }

        var member = await _userLookup.GetByIdAsync(domainEvent.UserId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(member?.Email))
        {
            try
            {
                if (domainEvent.RequiresPayment)
                {
                    await _email.SendEmailAsync(
                        member.Email,
                        $"Extension Approved: {domainEvent.BookingReference}",
                        $"<p>Hello {member.FirstName},</p>" +
                        $"<p>Great news! Your extension request for <strong>{title}</strong> has been approved.</p>" +
                        $"<p>Additional charge: <strong>INR {domainEvent.ExtraAmount:F2}</strong>.</p>" +
                        "<p>Please log in and complete payment to confirm the extension.</p>");
                }
                else
                {
                    await _email.SendEmailAsync(
                        member.Email,
                        $"Extension Confirmed: {domainEvent.BookingReference}",
                        $"<p>Hello {member.FirstName},</p>" +
                        $"<p>Great news! Your extension request for <strong>{title}</strong> has been approved and confirmed.</p>" +
                        "<p>Your active parking pass covered the extension, so no additional payment is required.</p>");
                }
            }
            catch
            {
                // Ignore email failure so we don't break the outbox processor
            }
        }
    }
}

internal sealed class BookingExtensionRejectedNotificationHandler : IDomainEventHandler<BookingExtensionRejectedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly INotificationSender _notificationSender;
    private readonly IEmailService _email;

    public BookingExtensionRejectedNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        INotificationSender notificationSender,
        IEmailService email)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _notificationSender = notificationSender;
        _email = email;
    }

    public async Task HandleAsync(BookingExtensionRejectedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        var title = parking?.Title ?? "your parking space";
        var reason = domainEvent.Reason ?? "Rejected by parking owner";

        await _notificationSender.SendAsync(
            domainEvent.UserId,
            new NotificationSendRequest(
                NotificationType.BookingRejected.ToString(),
                "Extension Request Rejected",
                $"Your extension request for {title} was rejected. Reason: {reason}",
                Channels: new[] { "InApp" },
                Data: new Dictionary<string, string>
                {
                    { "BookingId", domainEvent.BookingId.ToString() },
                    { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                    { "Reason", reason },
                    { "Type", "Extension" }
                }),
            cancellationToken);

        if (domainEvent.VendorUserId is { } vendorId && vendorId != Guid.Empty)
        {
            await _notificationSender.SendAsync(
                vendorId,
                new NotificationSendRequest(
                    NotificationType.BookingRejected.ToString(),
                    "Extension Rejected",
                    $"You have rejected the extension for {domainEvent.BookingReference}",
                    Channels: new[] { "InApp" },
                    Data: new Dictionary<string, string>
                    {
                        { "BookingId", domainEvent.BookingId.ToString() },
                        { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                        { "Type", "Extension" },
                        { "Silent", "true" }
                    }),
                cancellationToken);
        }

        var member = await _userLookup.GetByIdAsync(domainEvent.UserId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(member?.Email))
        {
            try
            {
                await _email.SendEmailAsync(
                    member.Email,
                    $"Extension Rejected: {domainEvent.BookingReference}",
                    $"<p>Hello {member.FirstName},</p>" +
                    $"<p>We're sorry, but your extension request for <strong>{title}</strong> was rejected.</p>" +
                    $"<p><strong>Reason:</strong> {reason}</p>" +
                    "<p>Your original booking remains unchanged.</p>");
            }
            catch
            {
                // Ignore email failure so we don't break the outbox processor
            }
        }
    }
}

internal sealed class BookingExtensionConfirmedNotificationHandler : IDomainEventHandler<BookingExtensionConfirmedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly INotificationSender _notificationSender;
    private readonly IEmailService _email;

    public BookingExtensionConfirmedNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        INotificationSender notificationSender,
        IEmailService email)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _notificationSender = notificationSender;
        _email = email;
    }

    public async Task HandleAsync(BookingExtensionConfirmedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
        var title = parking?.Title ?? "your parking space";
        var member = await _userLookup.GetByIdAsync(domainEvent.UserId, cancellationToken);
        var memberName = string.IsNullOrWhiteSpace(member?.FullName) ? "A member" : member.FullName;

        if (domainEvent.ExtraAmount > 0)
        {
            // Paid extension finalized (after payment) — notify vendor of payment
            if (parking is not null)
            {
                await _notificationSender.SendAsync(
                    parking.OwnerId,
                    new NotificationSendRequest(
                        NotificationType.PaymentReceived.ToString(),
                        "Extension Payment Received",
                        $"{memberName} has paid ₹{domainEvent.ExtraAmount:F2} to extend booking {domainEvent.BookingReference}",
                        Channels: new[] { "InApp" },
                        Data: new Dictionary<string, string>
                        {
                            { "BookingId", domainEvent.BookingId.ToString() },
                            { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                            { "Amount", domainEvent.ExtraAmount.ToString("F2") },
                            { "Type", "Extension" },
                            // Stable key for inbox dedupe when outbox retries
                            { "PaymentId", $"ext:{domainEvent.BookingId:N}:{domainEvent.NewEndUtc.Ticks}" }
                        }),
                    cancellationToken);

                var owner = await _userLookup.GetByIdAsync(parking.OwnerId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(owner?.Email))
                {
                    try
                    {
                        await _email.SendEmailAsync(
                            owner.Email,
                            $"Extension Payment Received: {domainEvent.BookingReference}",
                            $"<p>Hello {owner.FirstName},</p>" +
                            $"<p>Extension payment of <strong>₹{domainEvent.ExtraAmount:F2}</strong> received for booking {domainEvent.BookingReference}.</p>" +
                            $"<p>New end time: <strong>{domainEvent.NewEndUtc:f}</strong>.</p>");
                    }
                    catch
                    {
                        // Ignore email failure so we don't break the outbox processor
                    }
                }
            }

            await _notificationSender.SendAsync(
                domainEvent.UserId,
                new NotificationSendRequest(
                    NotificationType.BookingConfirmed.ToString(),
                    "Extension Confirmed!",
                    $"Your payment of ₹{domainEvent.ExtraAmount:F2} was successful and your extension for {title} is confirmed.",
                    Channels: new[] { "InApp" },
                    Data: new Dictionary<string, string>
                    {
                        { "BookingId", domainEvent.BookingId.ToString() },
                        { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                        { "Type", "Extension" }
                    }),
                cancellationToken);
        }
        else if (parking is not null)
        {
            // Free / pass-covered extension confirmed at approve time
            await _notificationSender.SendAsync(
                domainEvent.UserId,
                new NotificationSendRequest(
                    NotificationType.BookingConfirmed.ToString(),
                    "Extension Confirmed!",
                    $"Your extension request for {title} was approved and confirmed with your parking pass pricing.",
                    Channels: new[] { "InApp" },
                    Data: new Dictionary<string, string>
                    {
                        { "BookingId", domainEvent.BookingId.ToString() },
                        { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                        { "Type", "Extension" }
                    }),
                cancellationToken);

            await _notificationSender.SendAsync(
                parking.OwnerId,
                new NotificationSendRequest(
                    NotificationType.BookingConfirmed.ToString(),
                    "Extension Approved",
                    $"You have approved the extension for {domainEvent.BookingReference}",
                    Channels: new[] { "InApp" },
                    Data: new Dictionary<string, string>
                    {
                        { "BookingId", domainEvent.BookingId.ToString() },
                        { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                        { "Type", "Extension" },
                        { "Silent", "true" }
                    }),
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(member?.Email))
        {
            try
            {
                await _email.SendEmailAsync(
                    member.Email,
                    $"Extension Confirmed: {domainEvent.BookingReference}",
                    $"<p>Hello {member.FirstName},</p>" +
                    $"<p>Your booking extension for <strong>{title}</strong> has been confirmed.</p>" +
                    $"<p>New end time: <strong>{domainEvent.NewEndUtc:f}</strong>." +
                    (domainEvent.ExtraAmount > 0
                        ? $" Additional charge: <strong>₹{domainEvent.ExtraAmount:F2}</strong>.</p>"
                        : "</p>"));
            }
            catch
            {
                // Ignore email failure so we don't break the outbox processor
            }
        }
    }
}

internal sealed class PaymentCompletedNotificationHandler : IDomainEventHandler<PaymentCompletedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly INotificationSender _notificationSender;
    private readonly IEmailService _email;
    private readonly ILogger<PaymentCompletedNotificationHandler> _logger;

    public PaymentCompletedNotificationHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        INotificationSender notificationSender,
        IEmailService email,
        ILogger<PaymentCompletedNotificationHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _notificationSender = notificationSender;
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentCompletedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        // Extension payments are handled by BookingExtensionConfirmedNotificationHandler.
        if (domainEvent.IsExtensionPayment)
            return;

        try
        {
            var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
            if (parking is null)
                return;

            var payer = await _userLookup.GetByIdAsync(domainEvent.UserId, cancellationToken);
            var payerName = payer?.FirstName ?? "A user";
            var amountText = domainEvent.Amount.ToString("F2");

            // InApp only here — emails are sent below. Keeps channel failures from
            // complicating delivery. Inbox dedupes by PaymentId/BookingId on outbox retry.
            await _notificationSender.SendAsync(
                parking.OwnerId,
                new NotificationSendRequest(
                    NotificationType.PaymentReceived.ToString(),
                    "Payment Received",
                    $"{payerName} has completed payment for booking {domainEvent.BookingReference}",
                    Channels: new[] { "InApp" },
                    Data: new Dictionary<string, string>
                    {
                        { "PaymentId", domainEvent.PaymentId.ToString() },
                        { "BookingId", domainEvent.BookingId.ToString() },
                        { "BookingReference", domainEvent.BookingReference ?? string.Empty },
                        { "Amount", amountText },
                        { "Type", "Booking" }
                    }),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(payer?.Email))
            {
                await _email.SendEmailAsync(
                    payer.Email,
                    $"Payment Receipt: {domainEvent.BookingReference}",
                    $"<p>Hello {payer.FirstName},</p><p>Thank you for your payment of <strong>₹{amountText}</strong> for booking {domainEvent.BookingReference}.</p><p>Your booking is now <strong>Confirmed</strong>.</p>");
            }

            var owner = await _userLookup.GetByIdAsync(parking.OwnerId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(owner?.Email))
            {
                await _email.SendEmailAsync(
                    owner.Email,
                    $"Payment Received: {domainEvent.BookingReference}",
                    $"<p>Hello {owner.FirstName},</p><p>You have received a payment of <strong>₹{amountText}</strong> from {payerName} for booking {domainEvent.BookingReference}.</p>");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment notification for booking {BookingId}", domainEvent.BookingId);
        }
    }
}

