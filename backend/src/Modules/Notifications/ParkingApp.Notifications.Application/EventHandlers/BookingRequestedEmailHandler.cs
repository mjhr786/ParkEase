using Microsoft.Extensions.Logging;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Events;

namespace ParkingApp.Notifications.Application.EventHandlers;

/// <summary>
/// Async email notifications for marketplace booking lifecycle (outbox / post-commit).
/// Keeps Create/Approve HTTP handlers free of SMTP latency.
/// Uses module contracts for user/parking reads (no Domain entity leakage).
/// </summary>
internal sealed class BookingRequestedEmailHandler : IDomainEventHandler<BookingRequestedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly IEmailService _email;
    private readonly ILogger<BookingRequestedEmailHandler> _logger;

    public BookingRequestedEmailHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        IEmailService email,
        ILogger<BookingRequestedEmailHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(BookingRequestedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
            if (parking == null)
                return;

            var member = await _userLookup.GetByIdAsync(domainEvent.UserId, cancellationToken);
            var owner = parking.OwnerId != Guid.Empty
                ? await _userLookup.GetByIdAsync(parking.OwnerId, cancellationToken)
                : null;

            var memberName = member != null ? member.FullName : "A member";
            if (string.IsNullOrWhiteSpace(memberName))
                memberName = "A member";

            var spaceTitle = parking.Title;
            var reference = domainEvent.BookingReference ?? domainEvent.BookingId.ToString("N")[..8];

            if (!string.IsNullOrWhiteSpace(owner?.Email))
            {
                await _email.SendEmailAsync(
                    owner.Email,
                    $"New Booking Request: {reference}",
                    $"<p>Hello {owner.FirstName},</p>" +
                    $"<p>You have a new booking request from {memberName} for <strong>{spaceTitle}</strong>.</p>" +
                    "<p>Please log in to your dashboard to approve or reject it.</p>",
                    isHtml: true);
            }

            if (!string.IsNullOrWhiteSpace(member?.Email))
            {
                await _email.SendEmailAsync(
                    member.Email,
                    $"Booking Requested: {reference}",
                    $"<p>Hello {member.FirstName},</p>" +
                    $"<p>Your booking request for <strong>{spaceTitle}</strong> has been sent.</p>" +
                    "<p>You will be notified once the owner approves it.</p>",
                    isHtml: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BookingRequested email failed for booking {BookingId}", domainEvent.BookingId);
            throw;
        }
    }
}

internal sealed class BookingApprovedEmailHandler : IDomainEventHandler<BookingApprovedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly IEmailService _email;
    private readonly ILogger<BookingApprovedEmailHandler> _logger;

    public BookingApprovedEmailHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        IEmailService email,
        ILogger<BookingApprovedEmailHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(BookingApprovedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
            var member = await _userLookup.GetByIdAsync(domainEvent.UserId, cancellationToken);
            if (string.IsNullOrWhiteSpace(member?.Email))
                return;

            var spaceTitle = parking?.Title ?? "your parking space";
            var reference = domainEvent.BookingReference ?? domainEvent.BookingId.ToString("N")[..8];

            if (domainEvent.RequiresPayment)
            {
                await _email.SendEmailAsync(
                    member.Email,
                    $"Booking Approved: {reference}",
                    $"<p>Hello {member.FirstName},</p>" +
                    $"<p>Great news! Your booking for <strong>{spaceTitle}</strong> has been approved.</p>" +
                    "<p>Please log in and complete your payment to confirm the reservation.</p>",
                    isHtml: true);
            }
            else
            {
                await _email.SendEmailAsync(
                    member.Email,
                    $"Booking Approved: {reference}",
                    $"<p>Hello {member.FirstName},</p>" +
                    $"<p>Great news! Your booking for <strong>{spaceTitle}</strong> has been approved.</p>",
                    isHtml: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BookingApproved email failed for booking {BookingId}", domainEvent.BookingId);
            throw;
        }
    }
}

internal sealed class BookingConfirmedEmailHandler : IDomainEventHandler<BookingConfirmedEvent>
{
    private readonly IParkingSpaceLookup _parkingSpaceLookup;
    private readonly IUserLookup _userLookup;
    private readonly IEmailService _email;
    private readonly ILogger<BookingConfirmedEmailHandler> _logger;

    public BookingConfirmedEmailHandler(
        IParkingSpaceLookup parkingSpaceLookup,
        IUserLookup userLookup,
        IEmailService email,
        ILogger<BookingConfirmedEmailHandler> logger)
    {
        _parkingSpaceLookup = parkingSpaceLookup;
        _userLookup = userLookup;
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(BookingConfirmedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            // Corporate confirms also raise this event; still useful for member confirmation email.
            var parking = await _parkingSpaceLookup.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
            var member = await _userLookup.GetByIdAsync(domainEvent.UserId, cancellationToken);
            if (string.IsNullOrWhiteSpace(member?.Email))
                return;

            var spaceTitle = parking?.Title ?? "parking";
            var reference = domainEvent.BookingReference ?? domainEvent.BookingId.ToString("N")[..8];

            await _email.SendEmailAsync(
                member.Email,
                $"Booking Confirmed: {reference}",
                $"<p>Hello {member.FirstName},</p>" +
                $"<p>Your booking for <strong>{spaceTitle}</strong> is confirmed.</p>" +
                $"<p>Reference: <strong>{reference}</strong></p>",
                isHtml: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BookingConfirmed email failed for booking {BookingId}", domainEvent.BookingId);
            throw;
        }
    }
}
