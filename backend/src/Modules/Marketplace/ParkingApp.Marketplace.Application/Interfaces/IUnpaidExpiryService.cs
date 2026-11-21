using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Application.Interfaces;

public sealed record ExpireAwaitingPaymentsResult(
    int ExpiredBookings,
    int ExpiredOrders,
    int NeedsRefundDrained,
    int ErrorsRetried);

public interface IUnpaidExpiryService
{
    Task<bool> ExpireIfDueAsync(Booking booking, CancellationToken cancellationToken = default);

    Task<ExpireAwaitingPaymentsResult> RunJobAsync(int batchSize, CancellationToken cancellationToken = default);
}
