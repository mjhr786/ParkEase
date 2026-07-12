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

    public UpdateBookingHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IParkingPassPricingService pricingService,
        IBookingAvailabilityService availability)
    {
        _unitOfWork = unitOfWork;
        _pricingService = pricingService;
        _availability = availability;
    }

    public UpdateBookingHandler(IMarketplaceUnitOfWork unitOfWork)
        : this(unitOfWork, new ParkingPassPricingService(unitOfWork), new BookingAvailabilityService(unitOfWork))
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

        try
        {
            booking.UpdateVehicleDetails(dto.VehicleType, dto.VehicleNumber, dto.VehicleModel);

            // If dates changed, validate availability then recalculate pricing
            if (dto.StartDateTime.HasValue || dto.EndDateTime.HasValue)
            {
                var startDateTime = (dto.StartDateTime ?? booking.StartDateTime).ToUtc();
                var endDateTime = (dto.EndDateTime ?? booking.EndDateTime).ToUtc();

                var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
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

            return new ApiResponse<BookingDto>(true, "Booking updated", booking.ToDto());
        }
        catch (ParkingApp.BuildingBlocks.Exceptions.DomainException ex)
        {
            return ParkingApp.Application.Common.DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}
