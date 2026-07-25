using ParkingApp.Marketplace.Contracts;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Application.Interfaces;

namespace ParkingApp.Marketplace.Infrastructure.ModuleAdapters;

/// <summary>
/// Marketplace booking adapter: cancel (contract) + stage/update (transitional persistence facade).
/// Does not call SaveChanges ΓÇö callers that share UnitOfWork must persist.
/// </summary>
internal sealed class MarketplaceBookingService : IMarketplaceBookingService, IMarketplaceBookingPersistence
{
    private readonly IMarketplaceUnitOfWork _marketplace;

    public MarketplaceBookingService(IMarketplaceUnitOfWork marketplace) => _marketplace = marketplace;

    public async Task<MarketplaceBookingCancelResult> CancelAsync(
        Guid bookingId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var booking = await _marketplace.Bookings.GetByIdWithDetailsAsync(bookingId, cancellationToken);
        if (booking is null || booking.IsDeleted)
        {
            return new MarketplaceBookingCancelResult(false, "Booking not found.", null);
        }

        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled
            or BookingStatus.Expired or BookingStatus.Rejected)
        {
            return new MarketplaceBookingCancelResult(
                false,
                $"Cannot cancel a booking in {booking.Status} status.",
                BookingLookup.Map(booking));
        }

        try
        {
            booking.Cancel(reason);
            _marketplace.Bookings.Update(booking);

            return new MarketplaceBookingCancelResult(true, "Booking cancelled.", BookingLookup.Map(booking));
        }
        catch (DomainException ex)
        {
            return new MarketplaceBookingCancelResult(false, ex.Message, BookingLookup.Map(booking));
        }
    }

    public Task StageNewAsync(Booking booking, CancellationToken cancellationToken = default) =>
        _marketplace.Bookings.AddAsync(booking, cancellationToken);

    public void Update(Booking booking) =>
        _marketplace.Bookings.Update(booking);

    public async Task<MarketplaceBookingCreateResult> StageCorporateBookingAsync(
        StageCorporateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        Booking booking;

        if (request.IsVisitor)
        {
            booking = Booking.CreateCorporateVisitor(
                request.UserId,
                request.ParkingSpaceId,
                request.StartUtc,
                request.EndUtc,
                request.Amount,
                request.VehicleNumber);
        }
        else
        {
            // Default to Car if not specified; ideally passed in if known, but for backward compat we use Car.
            booking = Booking.CreateCorporateEmployee(
                request.UserId,
                request.ParkingSpaceId,
                request.StartUtc,
                request.EndUtc,
                VehicleType.Car,
                request.Amount,
                request.VehicleNumber);
        }

        await _marketplace.Bookings.AddAsync(booking, cancellationToken);
        
        return new MarketplaceBookingCreateResult(booking.Id, booking.QRCode);
    }
}

