using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Application.Services;
using ParkingApp.BuildingBlocks.Extensions;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Handlers.Bookings;

public class UpdateBookingHandler : ICommandHandler<UpdateBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IParkingPassPricingService _pricingService;
    private readonly IBookingAvailabilityService _availability;
    private readonly ICacheService _cache;

    public UpdateBookingHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IParkingPassPricingService pricingService,
        IBookingAvailabilityService availability,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _pricingService = pricingService;
        _availability = availability;
        _cache = cache;
    }

    public UpdateBookingHandler(IMarketplaceUnitOfWork unitOfWork)
        : this(
            unitOfWork,
            new ParkingPassPricingService(unitOfWork),
            new BookingAvailabilityService(unitOfWork),
            // Tests / legacy ctor: no-op invalidation if cache not provided
            new NullCacheService())
    {
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(UpdateBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        if (booking.UserId != command.UserId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
        {
            return new ApiResponse<BookingDto>(false, "Cannot update this booking", null);
        }

        var dto = command.Dto;
        var datesChanged = dto.StartDateTime.HasValue || dto.EndDateTime.HasValue;
        Guid? vendorId = null;

        try
        {
            booking.UpdateVehicleDetails(dto.VehicleType, dto.VehicleNumber, dto.VehicleModel);

            // If dates changed, validate availability then recalculate pricing
            if (datesChanged)
            {
                var startDateTime = (dto.StartDateTime ?? booking.StartDateTime).ToUtc();
                var endDateTime = (dto.EndDateTime ?? booking.EndDateTime).ToUtc();

                var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
                vendorId = parking?.OwnerId;
                if (parking != null)
                {
                    var availability = await _availability.CanRescheduleAsync(
                        booking, parking, startDateTime, endDateTime, cancellationToken);
                    if (!availability.IsAllowed)
                    {
                        return new ApiResponse<BookingDto>(false, availability.ErrorMessage ?? "Booking not available", null);
                    }
                }
                else
                {
                    // Parking missing: keep legacy time-window-only checks
                    if (startDateTime < DateTime.UtcNow)
                        return new ApiResponse<BookingDto>(false, "Start date must be in the future", null);
                    if (endDateTime <= startDateTime)
                        return new ApiResponse<BookingDto>(false, "End date must be after start date", null);
                }

                booking.Reschedule(startDateTime, endDateTime);

                if (parking != null)
                {
                    var pricing = await _pricingService.CalculateAsync(
                        command.UserId,
                        parking,
                        startDateTime,
                        endDateTime,
                        booking.PricingType,
                        booking.DiscountCode,
                        booking.Id,
                        cancellationToken);

                    booking.ApplyPricing(
                        pricing.BaseAmount,
                        pricing.TaxAmount,
                        pricing.ServiceFee,
                        pricing.DiscountAmount,
                        pricing.TotalAmount,
                        pricing.ParkingPassId,
                        pricing.IsPassApplied ? null : booking.DiscountCode);
                }
            }

            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Reschedule does not raise domain events — must invalidate availability caches explicitly.
            if (datesChanged)
            {
                if (vendorId is null)
                {
                    var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
                    vendorId = parking?.OwnerId;
                }

                await CacheInvalidation.ForBookingChangeAsync(
                    _cache,
                    booking.ParkingSpaceId,
                    memberId: booking.UserId,
                    vendorId: vendorId,
                    cancellationToken);
            }

            return new ApiResponse<BookingDto>(true, "Booking updated", booking.ToDto());
        }
        catch (ParkingApp.BuildingBlocks.Exceptions.DomainException ex)
        {
            return ParkingApp.Application.Common.DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }

    /// <summary>Minimal no-op cache for legacy test constructors.</summary>
    private sealed class NullCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult(default(T?));
        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<long> IncrementAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default) => await factory();
        public Task<bool> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task ReleaseLockAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
