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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IParkingPassPricingService _pricingService;

    public UpdateBookingHandler(IUnitOfWork unitOfWork, IParkingPassPricingService pricingService)
    {
        _unitOfWork = unitOfWork;
        _pricingService = pricingService;
    }

    public UpdateBookingHandler(IUnitOfWork unitOfWork)
        : this(unitOfWork, new ParkingPassPricingService(unitOfWork))
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

        // Update fields
        if (dto.VehicleType.HasValue) booking.VehicleType = dto.VehicleType.Value;
        if (!string.IsNullOrEmpty(dto.VehicleNumber)) booking.VehicleNumber = dto.VehicleNumber.Trim().ToUpper();
        if (!string.IsNullOrEmpty(dto.VehicleModel)) booking.VehicleModel = dto.VehicleModel.Trim();

        // If dates changed, recalculate pricing
        if (dto.StartDateTime.HasValue || dto.EndDateTime.HasValue)
        {
            var startDateTime = (dto.StartDateTime ?? booking.StartDateTime).ToUtc();
            var endDateTime = (dto.EndDateTime ?? booking.EndDateTime).ToUtc();

            // Validate new dates
            if (startDateTime < DateTime.UtcNow)
            {
                return new ApiResponse<BookingDto>(false, "Start date must be in the future", null);
            }

            if (endDateTime <= startDateTime)
            {
                return new ApiResponse<BookingDto>(false, "End date must be after start date", null);
            }

            // Check for overlaps excluding current booking
            var hasOverlap = await _unitOfWork.Bookings.HasOverlappingBookingAsync(
                booking.ParkingSpaceId, startDateTime, endDateTime, booking.Id, cancellationToken);

            var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
            if (hasOverlap && parking != null)
            {
                var activeCount = await _unitOfWork.Bookings.GetActiveBookingsCountAsync(
                    booking.ParkingSpaceId, startDateTime, endDateTime, cancellationToken);
                if (activeCount >= parking.TotalSpots)
                {
                    return new ApiResponse<BookingDto>(false, "No spots available for new dates", null);
                }
            }

            booking.StartDateTime = startDateTime;
            booking.EndDateTime = endDateTime;

            // Recalculate pricing
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

                booking.BaseAmount = pricing.BaseAmount;
                booking.TaxAmount = pricing.TaxAmount;
                booking.ServiceFee = pricing.ServiceFee;
                booking.DiscountAmount = pricing.DiscountAmount;
                booking.TotalAmount = pricing.TotalAmount;
                booking.ParkingPassId = pricing.ParkingPassId;
                booking.DiscountCode = pricing.IsPassApplied ? null : booking.DiscountCode;
            }
        }

        _unitOfWork.Bookings.Update(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<BookingDto>(true, "Booking updated", booking.ToDto());
    }
}
