using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.Services;
using ParkingApp.Messaging.Contracts.Enums;
using NotificationType = ParkingApp.Messaging.Contracts.Enums.NotificationType;

namespace ParkingApp.Marketplace.Application.Services;

/// <summary>
/// Finds InProgress bookings past EndDateTime + grace:
/// notifies once, assesses/increases overstay fees, and optionally auto check-outs.
/// </summary>
internal sealed class OverstayDetectionService : IOverstayDetectionService
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationSender _notificationSender;
    private readonly IOptionsMonitor<LprAccessOptions> _options;
    private readonly ILogger<OverstayDetectionService> _logger;

    public OverstayDetectionService(
        IMarketplaceUnitOfWork unitOfWork,
        INotificationSender notificationSender,
        IOptionsMonitor<LprAccessOptions> options,
        ILogger<OverstayDetectionService> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationSender = notificationSender;
        _options = options;
        _logger = logger;
    }

    public async Task<OverstayDetectionResult> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var overstayOpts = _options.CurrentValue.Overstay;
        var graceMinutes = Math.Clamp(overstayOpts.GraceMinutes, 0, 24 * 60);
        var autoMinutes = Math.Clamp(overstayOpts.AutoCheckOutMinutes, 0, 7 * 24 * 60);
        var grace = TimeSpan.FromMinutes(graceMinutes);
        var asOf = DateTime.UtcNow - grace;
        var take = Math.Clamp(batchSize, 1, 200);
        var now = DateTime.UtcNow;

        var overdue = await _unitOfWork.Bookings.GetOverdueInProgressAsync(asOf, take, cancellationToken);
        var notified = 0;
        var feesAssessed = 0;
        var autoCheckedOut = 0;

        foreach (var booking in overdue)
        {
            var title = booking.ParkingSpace?.Title ?? "parking";
            var minutesLate = Math.Max(0, (int)(now - booking.EndDateTime).TotalMinutes);
            var reference = booking.BookingReference ?? booking.Id.ToString("N")[..8];
            var changed = false;

            // 1) One-time alert — ask guest to extend (if available) or check out
            if (booking.TryMarkOverstayNotified(now))
            {
                changed = true;
                try
                {
                    var canExtend = CanRequestExtension(booking);
                    var guestData = BuildGuestOverstayData(booking.Id, canExtend);
                    await _notificationSender.SendAsync(
                        booking.UserId,
                        new NotificationSendRequest(
                            NotificationType.SystemAlert.ToString(),
                            canExtend ? "Overstay — extend or check out" : "Overstay — please check out",
                            BuildOverstayAlertMessage(overstayOpts, title, reference, minutesLate, canExtend),
                            Channels: new[] { "InApp" },
                            Data: guestData),
                        cancellationToken);

                    if (booking.ParkingSpace is { OwnerId: var ownerId }
                        && ownerId != Guid.Empty
                        && ownerId != booking.UserId)
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

                    notified++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed overstay notify for booking {BookingId}", booking.Id);
                }
            }

            // 2a) EV idle fee for charger-hogging on EV bookings
            if (booking.IncludeEvCharging && booking.ParkingSpace is { HasEvCharging: true } evSpace)
            {
                var idle = EvChargingFeeCalculator.CalculateIdleFee(
                    booking.EndDateTime,
                    now,
                    evSpace.EvIdleGraceMinutes,
                    evSpace.EvIdleRatePerHour);
                if (idle.HasFee && booking.ApplyEvIdleFee(idle.Fee, now))
                    changed = true;
            }

            // 2) Fee assessment (can increase as overstay continues; final top-up before auto check-out)
            if (overstayOpts.FeesEnabled && booking.ParkingSpace is not null)
            {
                if (OverstayFeeAssessor.TryAssess(booking, overstayOpts, now, out var calc) && calc.HasFee)
                {
                    changed = true;
                    feesAssessed++;

                    try
                    {
                        var canExtend = CanRequestExtension(booking);
                        var feeMsg = canExtend
                            ? $"An overstay fee of {calc.Fee:0.00} was added to booking {reference} (~{calc.BillableMinutes} billable min at {title}). Extend your booking to stay longer, or check out to stop further fees."
                            : $"An overstay fee of {calc.Fee:0.00} was added to booking {reference} (~{calc.BillableMinutes} billable min at {title}). Please check out to stop further fees.";

                        await _notificationSender.SendAsync(
                            booking.UserId,
                            new NotificationSendRequest(
                                NotificationType.SystemAlert.ToString(),
                                canExtend ? "Overstay fee — extend or check out" : "Overstay fee — please check out",
                                feeMsg,
                                Channels: new[] { "InApp" },
                                Data: BuildGuestOverstayData(booking.Id, canExtend, calc.Fee)),
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed overstay fee notify for booking {BookingId}", booking.Id);
                    }

                    _logger.LogInformation(
                        "Overstay fee {Fee} assessed on booking {BookingId} ({Minutes} min)",
                        calc.Fee, booking.Id, calc.BillableMinutes);
                }
            }

            // 3) Auto check-out after grace + AutoCheckOutMinutes
            if (overstayOpts.AutoCheckOutEnabled
                && booking.Status == BookingStatus.InProgress
                && ShouldAutoCheckOut(booking.EndDateTime, now, graceMinutes, autoMinutes))
            {
                try
                {
                    // Final fee top-up at auto check-out time
                    if (overstayOpts.FeesEnabled && booking.ParkingSpace is not null)
                        OverstayFeeAssessor.TryAssess(booking, overstayOpts, now, out _);

                    booking.CheckOut(now);
                    changed = true;
                    autoCheckedOut++;

                    var feeNote = booking.OverstayFeeAmount > 0
                        ? $" Overstay fee on booking: {booking.OverstayFeeAmount:0.00}."
                        : string.Empty;

                    await _notificationSender.SendAsync(
                        booking.UserId,
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

                    if (booking.ParkingSpace is { OwnerId: var ownerId }
                        && ownerId != Guid.Empty
                        && ownerId != booking.UserId)
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

                    _logger.LogInformation(
                        "Auto check-out booking {BookingId} after overstay (fee={Fee})",
                        booking.Id, booking.OverstayFeeAmount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed auto check-out for booking {BookingId}", booking.Id);
                }
            }

            if (changed)
                _unitOfWork.Bookings.Update(booking);
        }

        if (notified > 0 || feesAssessed > 0 || autoCheckedOut > 0 || overdue.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OverstayDetectionResult(notified, overdue.Count, feesAssessed, autoCheckedOut);
    }

    /// <summary>
    /// Auto check-out when now &gt;= End + Grace + AutoCheckOutMinutes.
    /// </summary>
    internal static bool ShouldAutoCheckOut(
        DateTime endDateTimeUtc,
        DateTime nowUtc,
        int graceMinutes,
        int autoCheckOutMinutes)
    {
        var cutoff = endDateTimeUtc
            .AddMinutes(Math.Clamp(graceMinutes, 0, 24 * 60))
            .AddMinutes(Math.Clamp(autoCheckOutMinutes, 0, 7 * 24 * 60));
        return nowUtc >= cutoff;
    }

    /// <summary>
    /// Extension is available for InProgress stays that do not already have a pending extension request.
    /// </summary>
    internal static bool CanRequestExtension(ParkingApp.Marketplace.Domain.Entities.Booking booking) =>
        booking.Status == BookingStatus.InProgress
        && !booking.HasPendingExtension
        && booking.Status != BookingStatus.PendingExtension
        && booking.Status != BookingStatus.AwaitingExtensionPayment;

    private static Dictionary<string, string> BuildGuestOverstayData(
        Guid bookingId,
        bool canExtend,
        decimal? feeAmount = null)
    {
        var data = new Dictionary<string, string>
        {
            { "BookingId", bookingId.ToString() },
            { "Type", feeAmount.HasValue ? "booking.overstay.fee" : "booking.overstay" },
            { "CanExtend", canExtend ? "true" : "false" },
            { "ActionCheckout", "true" },
            { "ActionExtend", canExtend ? "true" : "false" },
            // Deep-link hints for web/mobile clients
            { "CheckoutPath", $"/bookings/{bookingId}" },
            { "ExtendPath", canExtend ? $"/bookings/{bookingId}?action=extend" : string.Empty }
        };

        if (feeAmount.HasValue)
            data["FeeAmount"] = feeAmount.Value.ToString("0.00");

        return data;
    }

    private static string BuildOverstayAlertMessage(
        LprOverstayOptions opts,
        string title,
        string reference,
        int minutesLate,
        bool canExtend)
    {
        var msg =
            $"Your booking at {title} (ref {reference}) ended {minutesLate} min ago and is still active.";

        if (canExtend)
        {
            msg += " Please open My Bookings to extend your stay (request more time) if you need to keep parking, or check out if you are leaving.";
        }
        else
        {
            msg += " An extension is not available right now (a request may already be pending). Please check out from My Bookings if you are leaving.";
        }

        if (opts.FeesEnabled)
            msg += " Overstay fees may apply until you check out.";

        if (opts.AutoCheckOutEnabled)
        {
            var totalMins = Math.Clamp(opts.GraceMinutes, 0, 24 * 60)
                            + Math.Clamp(opts.AutoCheckOutMinutes, 0, 7 * 24 * 60);
            msg += $" Auto check-out may occur after about {totalMins} minutes past the scheduled end.";
        }

        return msg;
    }
}
