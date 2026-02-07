using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Bookings;

public class CreateBookingHandler : ICommandHandler<CreateBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateBookingHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(CreateBookingCommand command, CancellationToken cancellationToken = default)
    {
        // Validate parking space exists and is active
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, cancellationToken);
        if (parking == null || !parking.IsActive)
        {
            return new ApiResponse<BookingDto>(false, "Parking space is not available", null);
        }

        // Validate dates
        if (command.StartDateTime < DateTime.UtcNow)
        {
            return new ApiResponse<BookingDto>(false, "Start date must be in the future", null);
        }

        if (command.EndDateTime <= command.StartDateTime)
        {
            return new ApiResponse<BookingDto>(false, "End date must be after start date", null);
        }

        // Check vehicle overlap across any parking space
        if (!string.IsNullOrWhiteSpace(command.VehicleNumber))
        {
            var userBookings = await _unitOfWork.Bookings.GetByUserIdAsync(command.UserId, cancellationToken);
            var vehicleOverlap = userBookings.FirstOrDefault(b =>
                !string.IsNullOrWhiteSpace(b.VehicleNumber) &&
                b.VehicleNumber.Trim().Equals(command.VehicleNumber.Trim(), StringComparison.OrdinalIgnoreCase) &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.AwaitingPayment ||
                 b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.InProgress) &&
                b.StartDateTime < command.EndDateTime && b.EndDateTime > command.StartDateTime
            );

            if (vehicleOverlap != null)
            {
                return new ApiResponse<BookingDto>(false,
                    $"Vehicle {command.VehicleNumber} is already booked during this time (Ref: {vehicleOverlap.BookingReference})", null);
            }
        }

        // Check parking space overlap
        var hasOverlap = await _unitOfWork.Bookings.HasOverlappingBookingAsync(
            command.ParkingSpaceId, command.StartDateTime, command.EndDateTime, null, cancellationToken);

        if (hasOverlap)
        {
            var activeCount = await _unitOfWork.Bookings.GetActiveBookingsCountAsync(
                command.ParkingSpaceId, command.StartDateTime, command.EndDateTime, cancellationToken);
            if (activeCount >= parking.TotalSpots)
            {
                return new ApiResponse<BookingDto>(false, "No spots available for the selected time", null);
            }
        }

        // Calculate pricing
        var duration = command.EndDateTime - command.StartDateTime;
        decimal baseAmount = command.PricingType switch
        {
            PricingType.Hourly => parking.HourlyRate * (decimal)Math.Ceiling(duration.TotalHours),
            PricingType.Daily => parking.DailyRate * (decimal)Math.Ceiling(duration.TotalDays),
            PricingType.Weekly => parking.WeeklyRate * (decimal)Math.Ceiling(duration.TotalDays / 7),
            PricingType.Monthly => parking.MonthlyRate * (decimal)Math.Ceiling(duration.TotalDays / 30),
            _ => parking.HourlyRate * (decimal)Math.Ceiling(duration.TotalHours)
        };

        var taxAmount = baseAmount * 0.18m; // 18% GST
        var serviceFee = baseAmount * 0.05m; // 5% service fee
        var totalAmount = baseAmount + taxAmount + serviceFee;

        // Create booking
        var booking = new Booking
        {
            UserId = command.UserId,
            ParkingSpaceId = command.ParkingSpaceId,
            StartDateTime = command.StartDateTime,
            EndDateTime = command.EndDateTime,
            PricingType = command.PricingType,
            VehicleType = command.VehicleType,
            VehicleNumber = command.VehicleNumber?.Trim(),
            VehicleModel = command.VehicleModel?.Trim(),
            BaseAmount = baseAmount,
            TaxAmount = taxAmount,
            ServiceFee = serviceFee,
            TotalAmount = totalAmount,
            DiscountCode = command.DiscountCode,
            Status = BookingStatus.Pending,
            BookingReference = $"BK{DateTime.UtcNow:yyyyMMdd}{Guid.NewGuid().ToString()[..6].ToUpper()}"
        };

        await _unitOfWork.Bookings.AddAsync(booking, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<BookingDto>(true, "Booking created successfully", booking.ToDto());
    }
}

public class CancelBookingHandler : ICommandHandler<CancelBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CancelBookingHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(CancelBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        if (booking.UserId != command.UserId)
        {
            return new ApiResponse<BookingDto>(false, "You can only cancel your own bookings", null);
        }

        try
        {
            booking.Cancel(command.Reason);
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ApiResponse<BookingDto>(true, "Booking cancelled", booking.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse<BookingDto>(false, ex.Message, null);
        }
    }
}

public class ApproveBookingHandler : ICommandHandler<ApproveBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public ApproveBookingHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(ApproveBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        if (booking.ParkingSpace.OwnerId != command.VendorId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        try
        {
            booking.AwaitPayment();
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ApiResponse<BookingDto>(true, "Booking approved, awaiting payment", booking.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse<BookingDto>(false, ex.Message, null);
        }
    }
}

public class RejectBookingHandler : ICommandHandler<RejectBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public RejectBookingHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(RejectBookingCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        if (booking.ParkingSpace.OwnerId != command.VendorId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        try
        {
            booking.Reject(command.Reason ?? "Rejected by vendor");
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ApiResponse<BookingDto>(true, "Booking rejected", booking.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse<BookingDto>(false, ex.Message, null);
        }
    }
}

public class CheckInHandler : ICommandHandler<CheckInCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CheckInHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(CheckInCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        if (booking.UserId != command.UserId)
        {
            return new ApiResponse<BookingDto>(false, "You can only check in to your own bookings", null);
        }

        try
        {
            booking.CheckIn();
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ApiResponse<BookingDto>(true, "Checked in successfully", booking.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse<BookingDto>(false, ex.Message, null);
        }
    }
}

public class CheckOutHandler : ICommandHandler<CheckOutCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CheckOutHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(CheckOutCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        if (booking.UserId != command.UserId)
        {
            return new ApiResponse<BookingDto>(false, "You can only check out from your own bookings", null);
        }

        try
        {
            booking.CheckOut();
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ApiResponse<BookingDto>(true, "Checked out successfully", booking.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse<BookingDto>(false, ex.Message, null);
        }
    }
}
