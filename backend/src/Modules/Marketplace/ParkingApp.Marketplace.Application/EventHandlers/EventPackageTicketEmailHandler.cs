using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.EventHandlers;

/// <summary>
/// After an event-package booking is confirmed (free or paid), email a QR ticket PDF.
/// </summary>
internal sealed class EventPackageTicketEmailHandler : IDomainEventHandler<BookingConfirmedEvent>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IUserLookup _userLookup;
    private readonly IEmailService _email;
    private readonly IEventPackageTicketPdfService _ticketPdf;
    private readonly ILogger<EventPackageTicketEmailHandler> _logger;

    public EventPackageTicketEmailHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IUserLookup userLookup,
        IEmailService email,
        IEventPackageTicketPdfService ticketPdf,
        ILogger<EventPackageTicketEmailHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _userLookup = userLookup;
        _email = email;
        _ticketPdf = ticketPdf;
        _logger = logger;
    }

    public async Task HandleAsync(BookingConfirmedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(domainEvent.BookingId, cancellationToken);
            if (booking is null || booking.EventParkingPackageId is null)
                return;

            var package = await _unitOfWork.EventParkingPackages.GetByIdWithSpaceAsync(
                booking.EventParkingPackageId.Value,
                cancellationToken);
            if (package is null)
                return;

            if (booking.EnsureAccessPass())
            {
                _unitOfWork.Bookings.Update(booking);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(booking.QRCode))
                return;

            var member = await _userLookup.GetByIdAsync(booking.UserId, cancellationToken);
            if (string.IsNullOrWhiteSpace(member?.Email))
            {
                _logger.LogDebug(
                    "Skip event ticket email for booking {BookingId}: no member email",
                    booking.Id);
                return;
            }

            var parkingTitle = package.ParkingSpace?.Title
                ?? booking.ParkingSpace?.Title
                ?? "Parking";
            var parkingAddress = package.ParkingSpace?.Address ?? booking.ParkingSpace?.Address;

            var pdfBytes = _ticketPdf.GenerateTicketPdf(new EventPackageTicketContent(
                booking.BookingReference ?? booking.Id.ToString("N")[..8],
                booking.QRCode!,
                package.Title,
                package.EventName,
                package.VenueName,
                package.ZoneName,
                parkingTitle,
                parkingAddress,
                package.EventStartUtc,
                package.EventEndUtc,
                package.AccessStartUtc,
                package.AccessEndUtc,
                booking.VehicleNumber,
                booking.TotalAmount));

            var eventLabel = package.EventName ?? package.Title;
            var zoneLine = string.IsNullOrWhiteSpace(package.ZoneName)
                ? string.Empty
                : $"<p>Zone: <strong>{System.Net.WebUtility.HtmlEncode(package.ZoneName)}</strong></p>";

            var html =
                $"<p>Hello {System.Net.WebUtility.HtmlEncode(member.FirstName)},</p>" +
                $"<p>Your event parking ticket for <strong>{System.Net.WebUtility.HtmlEncode(eventLabel)}</strong> is attached.</p>" +
                zoneLine +
                $"<p>Lot: <strong>{System.Net.WebUtility.HtmlEncode(parkingTitle)}</strong></p>" +
                $"<p>Access: {package.AccessStartUtc:u} → {package.AccessEndUtc:u}</p>" +
                $"<p>Reference: <strong>{System.Net.WebUtility.HtmlEncode(booking.BookingReference ?? "")}</strong></p>" +
                "<p>Show the QR code at the gate (or use Access pass in the app).</p>";

            var attachment = new EmailAttachment(
                $"ParkEase-Ticket-{booking.BookingReference ?? booking.Id.ToString("N")[..8]}.pdf",
                "application/pdf",
                pdfBytes);

            await _email.SendEmailAsync(
                member.Email,
                $"Event parking ticket: {eventLabel}",
                html,
                new[] { attachment },
                isHtml: true);

            _logger.LogInformation(
                "Event package ticket emailed for booking {BookingId} package {PackageId}",
                booking.Id,
                package.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Event package ticket email failed for booking {BookingId}", domainEvent.BookingId);
            throw;
        }
    }
}
