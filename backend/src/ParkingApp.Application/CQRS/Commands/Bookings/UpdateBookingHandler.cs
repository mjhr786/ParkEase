using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Handlers.Bookings;

public class UpdateBookingHandler : ICommandHandler<UpdateBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private const decimal TaxRate = 0.18m;
    private const decimal ServiceFeeRate = 0.10m;

    public UpdateBookingHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
            var startDateTime = dto.StartDateTime ?? booking.StartDateTime;
            var endDateTime = dto.EndDateTime ?? booking.EndDateTime;

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
                var (baseAmount, _, _) = CalculateBaseAmount(parking, startDateTime, endDateTime, booking.PricingType);
                booking.BaseAmount = baseAmount;
                booking.TaxAmount = Math.Round(baseAmount * TaxRate, 2);
                booking.ServiceFee = Math.Round(baseAmount * ServiceFeeRate, 2);
                booking.TotalAmount = booking.BaseAmount + booking.TaxAmount + booking.ServiceFee - booking.DiscountAmount;
            }
        }

        _unitOfWork.Bookings.Update(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<BookingDto>(true, "Booking updated", booking.ToDto());
    }

    private (decimal BaseAmount, decimal DiscountAmount, decimal FinalAmount) CalculateBaseAmount(
        ParkingApp.Domain.Entities.ParkingSpace space,
        DateTime startDateTime,
        DateTime endDateTime,
        PricingType pricingType)
    {
        var duration = endDateTime - startDateTime;
        var totalHours = Math.Ceiling(duration.TotalHours);
        var totalDays = Math.Ceiling(duration.TotalDays);
        var totalMonths = Math.Ceiling(duration.TotalDays / 30);

        var amount = pricingType switch
        {
            PricingType.Hourly => space.HourlyRate * (decimal)totalHours,
            PricingType.Daily => space.DailyRate > 0 ? space.DailyRate : (space.HourlyRate * 24),
            PricingType.Weekly => space.WeeklyRate > 0 ? space.WeeklyRate : ((space.DailyRate > 0 ? space.DailyRate : (space.HourlyRate * 24)) * 7),
            PricingType.Monthly => space.MonthlyRate > 0 ? space.MonthlyRate : ((space.DailyRate > 0 ? space.DailyRate : (space.HourlyRate * 24)) * 30),
            _ => space.HourlyRate * (decimal)totalHours
        };

        return (amount, 0, amount);
    }
}
