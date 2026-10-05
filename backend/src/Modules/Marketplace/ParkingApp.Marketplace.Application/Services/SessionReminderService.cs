using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Marketplace.Application.Services;

/// <summary>
/// Sends one-time "session ending soon" in-app alerts with optional Extend CTA.
/// </summary>
internal sealed class SessionReminderService : ISessionReminderService
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IOptionsMonitor<SessionReminderOptions> _options;
    private readonly ILogger<SessionReminderService> _logger;
    private readonly TimeProvider _timeProvider;

    public SessionReminderService(
        IMarketplaceUnitOfWork unitOfWork,
        ICacheService cache,
        IOptionsMonitor<SessionReminderOptions> options,
        ILogger<SessionReminderService> logger,
        TimeProvider timeProvider)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<SessionReminderResult> ProcessAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
            return new SessionReminderResult(0, 0);

        var leadMinutes = Math.Clamp(opts.LeadMinutes, 1, 24 * 60);
        var take = Math.Clamp(batchSize, 1, 200);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var windowEnd = now.AddMinutes(leadMinutes);

        var lockKey = "job:session-reminder";
        var acquired = await _cache.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken);
        if (!acquired)
        {
            _logger.LogInformation("Job {JobName} is already running. Skipping.", lockKey);
            return new SessionReminderResult(0, 0);
        }

        try
        {
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

            if (!booking.TryMarkSessionEndReminded(now))
                continue;

            _unitOfWork.Bookings.Update(booking);
            notified++;
            _logger.LogInformation(
                "Session end reminder staged for outbox on booking {BookingId} (~{Minutes} min left)",
                booking.Id,
                minutesLeft);
        }

        if (notified > 0 || candidates.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SessionReminderResult(notified, candidates.Count);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, CancellationToken.None);
        }
    }

    /// <summary>
    /// Extension available when Confirmed/InProgress without a pending extension workflow.
    /// </summary>
    internal static bool CanRequestExtension(ParkingApp.Marketplace.Domain.Entities.Booking booking) =>
        (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.InProgress)
        && !booking.HasPendingExtension;
}
