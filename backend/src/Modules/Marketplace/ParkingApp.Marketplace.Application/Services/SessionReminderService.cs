using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using NotificationType = ParkingApp.Messaging.Contracts.Enums.NotificationType;

namespace ParkingApp.Marketplace.Application.Services;

/// <summary>
/// Sends one-time "session ending soon" in-app alerts with optional Extend CTA.
/// </summary>
internal sealed class SessionReminderService : ISessionReminderService
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationSender _notificationSender;
    private readonly IOptionsMonitor<SessionReminderOptions> _options;
    private readonly ILogger<SessionReminderService> _logger;

    public SessionReminderService(
        IMarketplaceUnitOfWork unitOfWork,
        INotificationSender notificationSender,
        IOptionsMonitor<SessionReminderOptions> options,
        ILogger<SessionReminderService> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationSender = notificationSender;
        _options = options;
        _logger = logger;
    }

    public async Task<SessionReminderResult> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
            return new SessionReminderResult(0, 0);

        var leadMinutes = Math.Clamp(opts.LeadMinutes, 1, 24 * 60);
        var take = Math.Clamp(batchSize, 1, 200);
        var now = DateTime.UtcNow;
        var windowEnd = now.AddMinutes(leadMinutes);

        var candidates = await _unitOfWork.Bookings.GetEndingSoonForReminderAsync(
            now,
            windowEnd,
            take,
            cancellationToken);

        var notified = 0;

        foreach (var booking in candidates)
        {
            if (booking.SessionEndRemindedAt.HasValue)
                continue;

            var title = booking.ParkingSpace?.Title ?? "parking";
            var reference = booking.BookingReference ?? booking.Id.ToString("N")[..8];
            var minutesLeft = Math.Max(1, (int)Math.Ceiling((booking.EndDateTime - now).TotalMinutes));
            var canExtend = CanRequestExtension(booking);

            try
            {
                await _notificationSender.SendAsync(
                    booking.UserId,
                    new NotificationSendRequest(
                        NotificationType.SystemAlert.ToString(),
                        canExtend
                            ? $"Parking ends in ~{minutesLeft} min — extend?"
                            : $"Parking ends in ~{minutesLeft} min",
                        BuildMessage(title, reference, minutesLeft, canExtend),
                        Channels: new[] { "InApp" },
                        Data: BuildGuestData(booking.Id, canExtend, minutesLeft)),
                    cancellationToken);

                if (!booking.TryMarkSessionEndReminded(now))
                    continue;

                _unitOfWork.Bookings.Update(booking);
                notified++;
                _logger.LogInformation(
                    "Session end reminder sent for booking {BookingId} (~{Minutes} min left)",
                    booking.Id,
                    minutesLeft);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed session end reminder for booking {BookingId}", booking.Id);
            }
        }

        if (notified > 0 || candidates.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SessionReminderResult(notified, candidates.Count);
    }

    /// <summary>
    /// Extension available when Confirmed/InProgress without a pending extension workflow.
    /// </summary>
    internal static bool CanRequestExtension(ParkingApp.Marketplace.Domain.Entities.Booking booking) =>
        (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.InProgress)
        && !booking.HasPendingExtension;

    private static Dictionary<string, string> BuildGuestData(Guid bookingId, bool canExtend, int minutesLeft)
    {
        return new Dictionary<string, string>
        {
            { "BookingId", bookingId.ToString() },
            { "Type", "booking.session.ending" },
            { "CanExtend", canExtend ? "true" : "false" },
            { "ActionExtend", canExtend ? "true" : "false" },
            { "ActionCheckout", bookingId != Guid.Empty ? "true" : "false" },
            { "MinutesLeft", minutesLeft.ToString() },
            { "CheckoutPath", $"/bookings/{bookingId}" },
            { "ExtendPath", canExtend ? $"/bookings/{bookingId}?action=extend" : string.Empty }
        };
    }

    private static string BuildMessage(string title, string reference, int minutesLeft, bool canExtend)
    {
        var msg =
            $"Your booking at {title} (ref {reference}) ends in about {minutesLeft} minute(s).";

        if (canExtend)
            return msg + " Tap Extend to stay longer, or Check out when you leave.";

        return msg + " Open your booking for details.";
    }
}
