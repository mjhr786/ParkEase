using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.Services;
using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Domain.Events;

namespace ParkingApp.Marketplace.Application.Services;

/// <summary>
/// Finds InProgress bookings past EndDateTime + grace:
/// notifies once, assesses/increases overstay fees, and optionally auto check-outs.
/// </summary>
internal sealed class OverstayDetectionService : IOverstayDetectionService
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IOptionsMonitor<LprAccessOptions> _options;
    private readonly ILogger<OverstayDetectionService> _logger;
    private readonly TimeProvider _timeProvider;

    public OverstayDetectionService(
        IMarketplaceUnitOfWork unitOfWork,
        ICacheService cache,
        IOptionsMonitor<LprAccessOptions> options,
        ILogger<OverstayDetectionService> logger,
        TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<OverstayDetectionResult> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var overstayOpts = _options.CurrentValue.Overstay;
        var graceMinutes = Math.Clamp(overstayOpts.GraceMinutes, 0, 24 * 60);
        var autoMinutes = Math.Clamp(overstayOpts.AutoCheckOutMinutes, 0, 7 * 24 * 60);
        var grace = TimeSpan.FromMinutes(graceMinutes);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var asOf = now - grace;
        var take = Math.Clamp(batchSize, 1, 200);

        var lockKey = "job:overstay-detection";
        var acquired = await _cache.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken);
        if (!acquired)
        {
            _logger.LogInformation("Job {JobName} is already running. Skipping.", lockKey);
            return new OverstayDetectionResult(0, 0, 0, 0);
        }

        try
        {
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
                notified++;
                _logger.LogInformation("Overstay alert staged for outbox on booking {BookingId}", booking.Id);
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
                // Final fee top-up at auto check-out time
                if (overstayOpts.FeesEnabled && booking.ParkingSpace is not null)
                    OverstayFeeAssessor.TryAssess(booking, overstayOpts, now, out _);

                booking.CheckOut(now);
                booking.AddDomainEvent(new BookingAutoCheckedOutEvent(booking.Id, booking.UserId, booking.ParkingSpaceId, booking.BookingReference, booking.OverstayFeeAmount));
                changed = true;
                autoCheckedOut++;

                _logger.LogInformation(
                    "Auto check-out booking {BookingId} after overstay (fee={Fee})",
                    booking.Id, booking.OverstayFeeAmount);
            }

            if (changed)
                _unitOfWork.Bookings.Update(booking);
        }

        if (notified > 0 || feesAssessed > 0 || autoCheckedOut > 0 || overdue.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OverstayDetectionResult(notified, overdue.Count, feesAssessed, autoCheckedOut);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, CancellationToken.None);
        }
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
}
