using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Events.Bookings;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.EventHandlers;

/// <summary>
/// Async email notifications for marketplace booking lifecycle (outbox / post-commit).
/// Keeps Create/Approve HTTP handlers free of SMTP latency.
/// </summary>
public sealed class BookingRequestedEmailHandler : IDomainEventHandler<BookingRequestedEvent>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IIdentityUnitOfWork _identity;
    private readonly IEmailService _email;
    private readonly ILogger<BookingRequestedEmailHandler> _logger;

    public BookingRequestedEmailHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IIdentityUnitOfWork identity,
        IEmailService email,
        ILogger<BookingRequestedEmailHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _identity = identity;
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(BookingRequestedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
            if (parking == null)
                return;

            var member = await _identity.Users.GetByIdAsync(domainEvent.UserId, cancellationToken);
            var owner = parking.Owner
                ?? (parking.OwnerId != Guid.Empty
                    ? await _identity.Users.GetByIdAsync(parking.OwnerId, cancellationToken)
                    : null);

            var memberName = member != null ? $"{member.FirstName} {member.LastName}".Trim() : "A member";
            var spaceTitle = parking.Title;
            var reference = domainEvent.BookingReference ?? domainEvent.BookingId.ToString("N")[..8];

            if (owner?.Email != null)
            {
                await _email.SendEmailAsync(
                    owner.Email,
                    $"New Booking Request: {reference}",
                    $"<p>Hello {owner.FirstName},</p>" +
                    $"<p>You have a new booking request from {memberName} for <strong>{spaceTitle}</strong>.</p>" +
                    "<p>Please log in to your dashboard to approve or reject it.</p>",
                    isHtml: true);
            }

            if (member?.Email != null)
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

public sealed class BookingApprovedEmailHandler : IDomainEventHandler<BookingApprovedEvent>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IIdentityUnitOfWork _identity;
    private readonly IEmailService _email;
    private readonly ILogger<BookingApprovedEmailHandler> _logger;

    public BookingApprovedEmailHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IIdentityUnitOfWork identity,
        IEmailService email,
        ILogger<BookingApprovedEmailHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _identity = identity;
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(BookingApprovedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
            var member = await _identity.Users.GetByIdAsync(domainEvent.UserId, cancellationToken);
            if (member?.Email == null)
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

public sealed class BookingConfirmedEmailHandler : IDomainEventHandler<BookingConfirmedEvent>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IIdentityUnitOfWork _identity;
    private readonly IEmailService _email;
    private readonly ILogger<BookingConfirmedEmailHandler> _logger;

    public BookingConfirmedEmailHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IIdentityUnitOfWork identity,
        IEmailService email,
        ILogger<BookingConfirmedEmailHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _identity = identity;
        _email = email;
        _logger = logger;
    }

    public async Task HandleAsync(BookingConfirmedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            // Corporate confirms also raise this event; still useful for member confirmation email.
            var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(domainEvent.ParkingSpaceId, cancellationToken);
            var member = await _identity.Users.GetByIdAsync(domainEvent.UserId, cancellationToken);
            if (member?.Email == null)
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
