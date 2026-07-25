using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Infrastructure.ModuleAdapters;

/// <summary>
/// Marketplace-side cascade for user account deletion.
/// Does not call SaveChanges — Identity DeleteUser owns the transaction.
/// </summary>
internal sealed class MarketplaceUserDataCleanup : IMarketplaceUserDataCleanup
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public MarketplaceUserDataCleanup(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task StageDeleteForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var bookingList = (await _unitOfWork.Bookings.GetByUserIdAsync(userId, cancellationToken)).ToList();
        foreach (var booking in bookingList)
        {
            var payments = await _unitOfWork.Payments.FindAsync(p => p.BookingId == booking.Id, cancellationToken);
            _unitOfWork.Payments.HardDeleteRange(payments);
        }

        _unitOfWork.Bookings.HardDeleteRange(bookingList);

        var reviews = await _unitOfWork.Reviews.FindAsync(r => r.UserId == userId, cancellationToken);
        _unitOfWork.Reviews.HardDeleteRange(reviews);

        var favorites = await _unitOfWork.Favorites.FindAsync(f => f.UserId == userId, cancellationToken);
        _unitOfWork.Favorites.HardDeleteRange(favorites);

        // Passes allocated to this user
        var passes = await _unitOfWork.ParkingPasses.GetByUserIdAsync(userId, cancellationToken);
        _unitOfWork.ParkingPasses.HardDeleteRange(passes);
    }
}
