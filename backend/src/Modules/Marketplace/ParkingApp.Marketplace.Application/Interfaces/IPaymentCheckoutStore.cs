using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Application.Interfaces;

public interface IPaymentCheckoutStore
{
    void Add(PaymentOrder order);
    void Add(PaymentWebhookEvent webhookEvent);
    void Update(PaymentOrder order);
    void Update(PaymentWebhookEvent webhookEvent);

    Task<PaymentOrder?> FindLiveOrderAsync(Guid bookingId, string purpose, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentOrder>> ListLiveOrdersAsync(Guid bookingId, string purpose, CancellationToken cancellationToken = default);
    Task<PaymentOrder?> FindCapturedOrderAsync(Guid bookingId, string purpose, CancellationToken cancellationToken = default);
    Task<PaymentOrder?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaymentOrder?> FindByProviderOrderIdAsync(string provider, string providerOrderId, CancellationToken cancellationToken = default);
    Task<PaymentOrder?> FindByProviderPaymentIdAsync(string provider, string providerPaymentId, CancellationToken cancellationToken = default);
    Task<PaymentWebhookEvent?> FindWebhookEventAsync(string provider, string providerEventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentWebhookEvent>> ListNeedsRefundAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentOrder>> ListOrdersForBookingPurposeAsync(Guid bookingId, string purpose, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentOrder>> ListLiveExpiredOrdersAsync(DateTime nowUtc, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentWebhookEvent>> ListRefundDrainAsync(DateTime errorCutoffUtc, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> ListExpiryCandidateBookingsAsync(DateTime nowUtc, int graceMinutes, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// SELECT … FOR UPDATE on PaymentOrders (and booking when the provider supports it).
    /// No-op lock when the backing store is not a relational DbContext.
    /// </summary>
    Task<PaymentOrder?> LockOrderAsync(Guid bookingId, string purpose, CancellationToken cancellationToken = default);

    /// <summary>Reloads a tracked booking from the database. No-op when the entity is detached.</summary>
    Task ReloadAsync(Booking booking, CancellationToken cancellationToken = default);
}
