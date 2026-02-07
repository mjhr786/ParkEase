using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.Services;

public class BookingService : IBookingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private const decimal TaxRate = 0.18m; // 18% tax
    private const decimal ServiceFeeRate = 0.05m; // 5% service fee

    public BookingService(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<ApiResponse<BookingDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(id, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        // Check authorization
        if (booking.UserId != userId && booking.ParkingSpace?.OwnerId != userId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        return new ApiResponse<BookingDto>(true, null, booking.ToDto());
    }

    public async Task<ApiResponse<BookingDto>> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByReferenceAsync(reference, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        return new ApiResponse<BookingDto>(true, null, booking.ToDto());
    }

    public async Task<ApiResponse<BookingListResultDto>> GetByUserAsync(Guid userId, BookingFilterDto? filter, CancellationToken cancellationToken = default)
    {
        var bookings = await _unitOfWork.Bookings.GetByUserIdAsync(userId, cancellationToken);
        var bookingList = bookings.ToList();

        // Apply filters
        if (filter != null)
        {
            if (filter.Status.HasValue)
                bookingList = bookingList.Where(b => b.Status == filter.Status.Value).ToList();
            if (filter.StartDate.HasValue)
                bookingList = bookingList.Where(b => b.StartDateTime >= filter.StartDate.Value).ToList();
            if (filter.EndDate.HasValue)
                bookingList = bookingList.Where(b => b.EndDateTime <= filter.EndDate.Value).ToList();
        }

        var page = filter?.Page ?? 1;
        var pageSize = filter?.PageSize ?? 20;
        var totalCount = bookingList.Count;
        
        var pagedBookings = bookingList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => b.ToDto())
            .ToList();

        var result = new BookingListResultDto(
            pagedBookings,
            totalCount,
            page,
            pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)
        );

        return new ApiResponse<BookingListResultDto>(true, null, result);
    }

    public async Task<ApiResponse<BookingListResultDto>> GetByParkingSpaceAsync(Guid parkingSpaceId, Guid ownerId, BookingFilterDto? filter, CancellationToken cancellationToken = default)
    {
        // Verify ownership
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != ownerId)
        {
            return new ApiResponse<BookingListResultDto>(false, "Unauthorized", null);
        }

        var bookings = await _unitOfWork.Bookings.GetByParkingSpaceIdAsync(parkingSpaceId, cancellationToken);
        var bookingList = bookings.Select(b => b.ToDto()).ToList();

        var page = filter?.Page ?? 1;
        var pageSize = filter?.PageSize ?? 20;

        var result = new BookingListResultDto(
            bookingList.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            bookingList.Count,
            page,
            pageSize,
            (int)Math.Ceiling((double)bookingList.Count / pageSize)
        );

        return new ApiResponse<BookingListResultDto>(true, null, result);
    }

    public async Task<ApiResponse<PriceBreakdownDto>> CalculatePriceAsync(PriceCalculationDto dto, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(dto.ParkingSpaceId, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<PriceBreakdownDto>(false, "Parking space not found", null);
        }

        var duration = dto.EndDateTime - dto.StartDateTime;
        var (baseAmount, durationValue, durationUnit) = CalculateBaseAmount(parking, dto.StartDateTime, dto.EndDateTime, dto.PricingType);

        var taxAmount = Math.Round(baseAmount * TaxRate, 2);
        var serviceFee = Math.Round(baseAmount * ServiceFeeRate, 2);
        var discountAmount = 0m;

        // Apply discount if code provided
        if (!string.IsNullOrEmpty(dto.DiscountCode))
        {
            discountAmount = ApplyDiscount(baseAmount, dto.DiscountCode);
        }

        var totalAmount = baseAmount + taxAmount + serviceFee - discountAmount;

        var result = new PriceBreakdownDto(
            baseAmount,
            taxAmount,
            serviceFee,
            discountAmount,
            totalAmount,
            $"{dto.PricingType} rate applied",
            durationValue,
            durationUnit
        );

        return new ApiResponse<PriceBreakdownDto>(true, null, result);
    }

    public async Task<ApiResponse<BookingDto>> CreateAsync(Guid userId, CreateBookingDto dto, CancellationToken cancellationToken = default)
    {
        // Validate parking space
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(dto.ParkingSpaceId, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<BookingDto>(false, "Parking space not found", null);
        }

        if (!parking.IsActive)
        {
            return new ApiResponse<BookingDto>(false, "Parking space is not available", null);
        }

        // Validate dates
        if (dto.StartDateTime < DateTime.UtcNow)
        {
            return new ApiResponse<BookingDto>(false, "Start date must be in the future", null);
        }

        // Check if the same vehicle is already booked during overlapping time (across any parking space)
        if (!string.IsNullOrWhiteSpace(dto.VehicleNumber))
        {
            var userExistingBookings = await _unitOfWork.Bookings.GetByUserIdAsync(userId, cancellationToken);
            var vehicleOverlapBooking = userExistingBookings.FirstOrDefault(b =>
                !string.IsNullOrWhiteSpace(b.VehicleNumber) &&
                b.VehicleNumber.Trim().Equals(dto.VehicleNumber.Trim(), StringComparison.OrdinalIgnoreCase) &&
                (b.Status == BookingStatus.Pending ||
                 b.Status == BookingStatus.AwaitingPayment ||
                 b.Status == BookingStatus.Confirmed ||
                 b.Status == BookingStatus.InProgress) &&
                // Check for time overlap
                b.StartDateTime < dto.EndDateTime && b.EndDateTime > dto.StartDateTime
            );

            if (vehicleOverlapBooking != null)
            {
                return new ApiResponse<BookingDto>(false,
                    $"Vehicle {dto.VehicleNumber} is already booked during this time period at another location (Ref: {vehicleOverlapBooking.BookingReference})",
                    null);
            }
        }

        // Check if user already has an active/pending booking for this parking space with overlapping time
        var userParkingBookings = await _unitOfWork.Bookings.GetByUserIdAsync(userId, cancellationToken);
        var duplicateBooking = userParkingBookings.FirstOrDefault(b =>
            b.ParkingSpaceId == dto.ParkingSpaceId &&
            (b.Status == BookingStatus.Pending || 
             b.Status == BookingStatus.AwaitingPayment || 
             b.Status == BookingStatus.Confirmed ||
             b.Status == BookingStatus.InProgress) &&
            // Check for time overlap
            b.StartDateTime < dto.EndDateTime && b.EndDateTime > dto.StartDateTime
        );

        if (duplicateBooking != null)
        {
            return new ApiResponse<BookingDto>(false, 
                $"You already have a booking for this parking space during this time period (Ref: {duplicateBooking.BookingReference})", 
                null);
        }

        // Check for overlapping bookings from other users
        var hasOverlap = await _unitOfWork.Bookings.HasOverlappingBookingAsync(
            dto.ParkingSpaceId, dto.StartDateTime, dto.EndDateTime, null, cancellationToken);

        if (hasOverlap)
        {
            // Check if spots are still available
            var activeBookingsCount = await _unitOfWork.Bookings.GetActiveBookingsCountAsync(
                dto.ParkingSpaceId, dto.StartDateTime, dto.EndDateTime, cancellationToken);

            if (activeBookingsCount >= parking.TotalSpots)
            {
                return new ApiResponse<BookingDto>(false, "No spots available for the selected time", null);
            }
        }

        // Calculate pricing
        var (baseAmount, _, _) = CalculateBaseAmount(parking, dto.StartDateTime, dto.EndDateTime, dto.PricingType);
        var taxAmount = Math.Round(baseAmount * TaxRate, 2);
        var serviceFee = Math.Round(baseAmount * ServiceFeeRate, 2);
        var discountAmount = ApplyDiscount(baseAmount, dto.DiscountCode);
        var totalAmount = baseAmount + taxAmount + serviceFee - discountAmount;

        // Create booking
        var booking = new Booking
        {
            UserId = userId,
            ParkingSpaceId = dto.ParkingSpaceId,
            StartDateTime = dto.StartDateTime,
            EndDateTime = dto.EndDateTime,
            PricingType = dto.PricingType,
            VehicleType = dto.VehicleType,
            VehicleNumber = dto.VehicleNumber?.Trim().ToUpper(),
            VehicleModel = dto.VehicleModel?.Trim(),
            BaseAmount = baseAmount,
            TaxAmount = taxAmount,
            ServiceFee = serviceFee,
            DiscountAmount = discountAmount,
            DiscountCode = dto.DiscountCode,
            TotalAmount = totalAmount,
            Status = BookingStatus.Pending,
            BookingReference = GenerateBookingReference()
        };

        await _unitOfWork.Bookings.AddAsync(booking, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with navigation properties
        booking = await _unitOfWork.Bookings.GetByIdAsync(booking.Id, cancellationToken);

        // Notify parking owner of new booking request
        var memberName = booking?.User != null ? $"{booking.User.FirstName} {booking.User.LastName}" : "A member";
        await _notificationService.NotifyUserAsync(
            parking.OwnerId,
            new NotificationDto(
                NotificationTypes.BookingRequested,
                "New Booking Request",
                $"New booking request from {memberName} for {parking.Title}",
                new { BookingId = booking!.Id, BookingReference = booking.BookingReference }
            ),
            cancellationToken);

        return new ApiResponse<BookingDto>(true, "Booking created. Awaiting owner approval.", booking.ToDto());
    }

    public async Task<ApiResponse<BookingDto>> UpdateAsync(Guid id, Guid userId, UpdateBookingDto dto, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(id, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        if (booking.UserId != userId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
        {
            return new ApiResponse<BookingDto>(false, "Cannot update this booking", null);
        }

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

    public async Task<ApiResponse<BookingDto>> CancelAsync(Guid id, Guid userId, CancelBookingDto dto, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(id, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        if (booking.UserId != userId && booking.ParkingSpace?.OwnerId != userId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
        {
            return new ApiResponse<BookingDto>(false, "Cannot cancel this booking", null);
        }

        // Calculate refund (full refund if cancelled > 24h before start)
        decimal refundAmount = 0;
        if (booking.Payment?.Status == PaymentStatus.Completed)
        {
            var hoursUntilStart = (booking.StartDateTime - DateTime.UtcNow).TotalHours;
            if (hoursUntilStart > 24)
            {
                refundAmount = booking.TotalAmount;
            }
            else if (hoursUntilStart > 2)
            {
                refundAmount = booking.TotalAmount * 0.5m; // 50% refund
            }
            // Less than 2 hours = no refund
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancellationReason = dto.Reason;
        booking.CancelledAt = DateTime.UtcNow;
        booking.RefundAmount = refundAmount;

        _unitOfWork.Bookings.Update(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify the other party about cancellation
        var recipientId = booking.UserId == userId ? booking.ParkingSpace?.OwnerId : booking.UserId;
        if (recipientId.HasValue)
        {
            await _notificationService.NotifyUserAsync(
                recipientId.Value,
                new NotificationDto(
                    NotificationTypes.BookingCancelled,
                    "Booking Cancelled",
                    $"Booking {booking.BookingReference} has been cancelled",
                    new { BookingId = id, BookingReference = booking.BookingReference }
                ),
                cancellationToken);
        }

        return new ApiResponse<BookingDto>(true, 
            refundAmount > 0 ? $"Booking cancelled. Refund of {refundAmount:C} will be processed." : "Booking cancelled.",
            booking.ToDto());
    }

    public async Task<ApiResponse<BookingDto>> CheckInAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(id, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        if (booking.UserId != userId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        if (booking.Status != BookingStatus.Confirmed)
        {
            return new ApiResponse<BookingDto>(false, "Booking must be confirmed to check in", null);
        }

        // Allow check-in within 30 minutes before start time
        var minutesUntilStart = (booking.StartDateTime - DateTime.UtcNow).TotalMinutes;
        if (minutesUntilStart > 30)
        {
            return new ApiResponse<BookingDto>(false, "Too early to check in", null);
        }

        booking.Status = BookingStatus.InProgress;
        booking.CheckInTime = DateTime.UtcNow;

        _unitOfWork.Bookings.Update(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify owner of check-in
        if (booking.ParkingSpace?.OwnerId != null)
        {
            await _notificationService.NotifyUserAsync(
                booking.ParkingSpace.OwnerId,
                new NotificationDto(
                    NotificationTypes.CheckIn,
                    "Guest Checked In",
                    $"{booking.User?.FirstName} has checked in at {booking.ParkingSpace.Title}",
                    new { BookingId = id, BookingReference = booking.BookingReference }
                ),
                cancellationToken);
        }

        return new ApiResponse<BookingDto>(true, "Checked in successfully", booking.ToDto());
    }

    public async Task<ApiResponse<BookingDto>> CheckOutAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(id, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        if (booking.UserId != userId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        if (booking.Status != BookingStatus.InProgress)
        {
            return new ApiResponse<BookingDto>(false, "Booking must be in progress to check out", null);
        }

        booking.Status = BookingStatus.Completed;
        booking.CheckOutTime = DateTime.UtcNow;

        _unitOfWork.Bookings.Update(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<BookingDto>(true, "Checked out successfully", booking.ToDto());
    }

    public async Task<ApiResponse<BookingListResultDto>> GetVendorBookingsAsync(Guid vendorId, BookingFilterDto? filter, CancellationToken cancellationToken = default)
    {
        // Get all parking spaces owned by this vendor
        var parkingSpaces = await _unitOfWork.ParkingSpaces.GetByOwnerIdAsync(vendorId, cancellationToken);
        var parkingSpaceIds = parkingSpaces.Select(p => p.Id).ToList();

        // Get all bookings for these parking spaces
        var allBookings = new List<Booking>();
        foreach (var parkingSpaceId in parkingSpaceIds)
        {
            var bookings = await _unitOfWork.Bookings.GetByParkingSpaceIdAsync(parkingSpaceId, cancellationToken);
            allBookings.AddRange(bookings);
        }

        var bookingList = allBookings.ToList();

        // Apply filters
        if (filter != null)
        {
            if (filter.Status.HasValue)
                bookingList = bookingList.Where(b => b.Status == filter.Status.Value).ToList();
            if (filter.StartDate.HasValue)
                bookingList = bookingList.Where(b => b.StartDateTime >= filter.StartDate.Value).ToList();
            if (filter.EndDate.HasValue)
                bookingList = bookingList.Where(b => b.EndDateTime <= filter.EndDate.Value).ToList();
        }

        // Order by created date (newest first)
        bookingList = bookingList.OrderByDescending(b => b.CreatedAt).ToList();

        var page = filter?.Page ?? 1;
        var pageSize = filter?.PageSize ?? 20;
        var totalCount = bookingList.Count;

        var pagedBookings = bookingList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => b.ToDto())
            .ToList();

        var result = new BookingListResultDto(
            pagedBookings,
            totalCount,
            page,
            pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)
        );

        return new ApiResponse<BookingListResultDto>(true, null, result);
    }

    public async Task<ApiResponse<BookingDto>> ApproveAsync(Guid id, Guid vendorId, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(id, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        // Verify the vendor owns this parking space
        if (booking.ParkingSpace?.OwnerId != vendorId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        if (booking.Status != BookingStatus.Pending)
        {
            return new ApiResponse<BookingDto>(false, "Only pending bookings can be approved", null);
        }

        booking.Status = BookingStatus.AwaitingPayment;
        
        _unitOfWork.Bookings.Update(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify member that their booking was approved
        await _notificationService.NotifyUserAsync(
            booking.UserId,
            new NotificationDto(
                NotificationTypes.BookingApproved,
                "Booking Approved!",
                $"Your booking for {booking.ParkingSpace?.Title} has been approved. Please complete payment.",
                new { BookingId = id, BookingReference = booking.BookingReference }
            ),
            cancellationToken);

        return new ApiResponse<BookingDto>(true, "Booking approved. Awaiting member payment.", booking.ToDto());
    }

    public async Task<ApiResponse<BookingDto>> RejectAsync(Guid id, Guid vendorId, string? reason, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(id, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        // Verify the vendor owns this parking space
        if (booking.ParkingSpace?.OwnerId != vendorId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        if (booking.Status != BookingStatus.Pending)
        {
            return new ApiResponse<BookingDto>(false, "Only pending bookings can be rejected", null);
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancellationReason = reason ?? "Rejected by parking owner";
        booking.CancelledAt = DateTime.UtcNow;
        
        _unitOfWork.Bookings.Update(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify member that their booking was rejected
        await _notificationService.NotifyUserAsync(
            booking.UserId,
            new NotificationDto(
                NotificationTypes.BookingRejected,
                "Booking Rejected",
                $"Your booking for {booking.ParkingSpace?.Title} was rejected. Reason: {booking.CancellationReason}",
                new { BookingId = id, BookingReference = booking.BookingReference, Reason = booking.CancellationReason }
            ),
            cancellationToken);

        return new ApiResponse<BookingDto>(true, "Booking rejected", booking.ToDto());
    }

    private static (decimal amount, int duration, string unit) CalculateBaseAmount(
        ParkingSpace parking, DateTime start, DateTime end, PricingType pricingType)
    {
        var duration = end - start;

        return pricingType switch
        {
            PricingType.Hourly => (
                Math.Ceiling((decimal)duration.TotalHours) * parking.HourlyRate,
                (int)Math.Ceiling(duration.TotalHours),
                "hours"
            ),
            PricingType.Daily => (
                Math.Ceiling((decimal)duration.TotalDays) * parking.DailyRate,
                (int)Math.Ceiling(duration.TotalDays),
                "days"
            ),
            PricingType.Weekly => (
                Math.Ceiling((decimal)duration.TotalDays / 7) * parking.WeeklyRate,
                (int)Math.Ceiling(duration.TotalDays / 7),
                "weeks"
            ),
            PricingType.Monthly => (
                Math.Ceiling((decimal)duration.TotalDays / 30) * parking.MonthlyRate,
                (int)Math.Ceiling(duration.TotalDays / 30),
                "months"
            ),
            _ => (0, 0, "")
        };
    }

    private static decimal ApplyDiscount(decimal baseAmount, string? discountCode)
    {
        if (string.IsNullOrEmpty(discountCode)) return 0;

        // Simple discount codes for demo
        return discountCode.ToUpper() switch
        {
            "FIRST10" => Math.Round(baseAmount * 0.10m, 2), // 10% off
            "PARK20" => Math.Round(baseAmount * 0.20m, 2), // 20% off
            "SAVE50" => 50m, // Flat ₹50 off
            _ => 0
        };
    }

    private static string GenerateBookingReference()
    {
        return $"PKG-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
    }
}
