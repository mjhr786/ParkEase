using Microsoft.EntityFrameworkCore;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Infrastructure.Persistence;

namespace ParkingApp.Marketplace.Infrastructure.Services;

internal sealed class PaymentCheckoutStore : IPaymentCheckoutStore
{
    private readonly IMarketplaceDbContext _db;

    public PaymentCheckoutStore(IMarketplaceDbContext db)
    {
        _db = db;
    }

    public void Add(PaymentOrder order) => _db.PaymentOrders.Add(order);

    public void Add(PaymentWebhookEvent webhookEvent) => _db.PaymentWebhookEvents.Add(webhookEvent);

    public void Update(PaymentOrder order) => _db.PaymentOrders.Update(order);

    public void Update(PaymentWebhookEvent webhookEvent) => _db.PaymentWebhookEvents.Update(webhookEvent);

    public Task<PaymentOrder?> FindLiveOrderAsync(
        Guid bookingId,
        string purpose,
        CancellationToken cancellationToken = default) =>
        _db.PaymentOrders.FirstOrDefaultAsync(
            o => o.BookingId == bookingId
                 && o.Purpose == purpose
                 && (o.Status == PaymentOrderStatus.Created || o.Status == PaymentOrderStatus.Pending),
            cancellationToken);

    public async Task<IReadOnlyList<PaymentOrder>> ListLiveOrdersAsync(
        Guid bookingId,
        string purpose,
        CancellationToken cancellationToken = default) =>
        await _db.PaymentOrders
            .Where(o => o.BookingId == bookingId
                        && o.Purpose == purpose
                        && (o.Status == PaymentOrderStatus.Created || o.Status == PaymentOrderStatus.Pending))
            .ToListAsync(cancellationToken);

    public Task<PaymentOrder?> FindCapturedOrderAsync(
        Guid bookingId,
        string purpose,
        CancellationToken cancellationToken = default) =>
        _db.PaymentOrders.FirstOrDefaultAsync(
            o => o.BookingId == bookingId
                 && o.Purpose == purpose
                 && o.Status == PaymentOrderStatus.Captured,
            cancellationToken);

    public Task<PaymentOrder?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.PaymentOrders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<PaymentOrder?> FindByProviderOrderIdAsync(
        string provider,
        string providerOrderId,
        CancellationToken cancellationToken = default) =>
        _db.PaymentOrders.FirstOrDefaultAsync(
            o => o.Provider == provider && o.ProviderOrderId == providerOrderId,
            cancellationToken);

    public Task<PaymentOrder?> FindByProviderPaymentIdAsync(
        string provider,
        string providerPaymentId,
        CancellationToken cancellationToken = default) =>
        _db.PaymentOrders.FirstOrDefaultAsync(
            o => o.Provider == provider && o.ProviderPaymentId == providerPaymentId,
            cancellationToken);

    public async Task ReloadAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        if (_db is not DbContext dbContext)
            return;

        var entry = dbContext.Entry(booking);
        if (entry.State == EntityState.Detached)
            return;

        await entry.ReloadAsync(cancellationToken);
    }

    public Task<PaymentWebhookEvent?> FindWebhookEventAsync(
        string provider,
        string providerEventId,
        CancellationToken cancellationToken = default) =>
        _db.PaymentWebhookEvents.FirstOrDefaultAsync(
            e => e.Provider == provider && e.ProviderEventId == providerEventId,
            cancellationToken);

    public async Task<IReadOnlyList<PaymentWebhookEvent>> ListNeedsRefundAsync(
        CancellationToken cancellationToken = default) =>
        await _db.PaymentWebhookEvents
            .Where(e => e.Outcome == "needs_refund" || e.Outcome == "rejected_mismatch")
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentOrder>> ListOrdersForBookingPurposeAsync(
        Guid bookingId,
        string purpose,
        CancellationToken cancellationToken = default) =>
        await _db.PaymentOrders
            .Where(o => o.BookingId == bookingId && o.Purpose == purpose)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentOrder>> ListLiveExpiredOrdersAsync(
        DateTime nowUtc,
        int take,
        CancellationToken cancellationToken = default) =>
        await _db.PaymentOrders
            .Where(o =>
                (o.Status == PaymentOrderStatus.Created || o.Status == PaymentOrderStatus.Pending)
                && o.ExpiresAtUtc != null
                && o.ExpiresAtUtc <= nowUtc)
            .OrderBy(o => o.ExpiresAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentWebhookEvent>> ListRefundDrainAsync(
        DateTime errorCutoffUtc,
        int take,
        CancellationToken cancellationToken = default) =>
        await _db.PaymentWebhookEvents
            .Where(e =>
                e.Outcome == "needs_refund"
                || e.Outcome == "rejected_mismatch"
                || (e.Outcome == "error" && e.ProcessedAtUtc == null && e.UpdatedAt != null && e.UpdatedAt <= errorCutoffUtc))
            .OrderBy(e => e.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> ListExpiryCandidateBookingsAsync(
        DateTime nowUtc,
        int graceMinutes,
        int take,
        CancellationToken cancellationToken = default)
    {
        var cutoff = nowUtc.AddMinutes(-graceMinutes);
        var bookingIds = await _db.PaymentOrders
            .Where(o => o.Rail == PaymentOrder.RailUpi && o.Purpose == "booking")
            .Select(o => o.BookingId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = new List<Booking>();
        foreach (var bookingId in bookingIds)
        {
            if (result.Count >= take)
                break;

            var booking = await _db.Bookings.FirstOrDefaultAsync(
                b => b.Id == bookingId && b.Status == BookingStatus.AwaitingPayment,
                cancellationToken);
            if (booking is null)
                continue;

            var orders = await _db.PaymentOrders
                .Where(o => o.BookingId == bookingId && o.Purpose == "booking")
                .ToListAsync(cancellationToken);

            if (orders.Any(o => o.Status == PaymentOrderStatus.Created || o.Status == PaymentOrderStatus.Pending))
                continue;

            var lastExpiry = orders
                .Where(o => o.Rail == PaymentOrder.RailUpi && o.ExpiresAtUtc != null)
                .Select(o => o.ExpiresAtUtc!.Value)
                .DefaultIfEmpty()
                .Max();
            if (lastExpiry == default || lastExpiry > cutoff)
                continue;

            result.Add(booking);
        }

        return result;
    }

    public async Task<PaymentOrder?> LockOrderAsync(
        Guid bookingId,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        if (_db is DbContext dbContext && CanForUpdate(dbContext))
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"SELECT 1 FROM ""PaymentOrders"" WHERE ""BookingId"" = {bookingId} AND ""Purpose"" = {purpose} AND ""IsDeleted"" = FALSE FOR UPDATE",
                cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"SELECT 1 FROM ""Bookings"" WHERE ""Id"" = {bookingId} FOR UPDATE",
                cancellationToken);
        }

        var orders = await _db.PaymentOrders
            .Where(o => o.BookingId == bookingId && o.Purpose == purpose)
            .ToListAsync(cancellationToken);

        return orders
            .OrderByDescending(o => o.IsLive)
            .ThenByDescending(o => o.CreatedAt)
            .FirstOrDefault();
    }

    private static bool CanForUpdate(DbContext dbContext) =>
        dbContext.Database.ProviderName is { } name
        && name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
}
