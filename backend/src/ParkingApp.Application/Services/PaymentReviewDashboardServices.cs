using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.Services;

public class PaymentAppService : IPaymentAppService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;

    public PaymentAppService(IUnitOfWork unitOfWork, IPaymentService paymentService, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _notificationService = notificationService;
    }

    public async Task<ApiResponse<PaymentDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(id, cancellationToken);
        if (payment == null)
        {
            return new ApiResponse<PaymentDto>(false, "Payment not found", null);
        }

        if (payment.UserId != userId)
        {
            return new ApiResponse<PaymentDto>(false, "Unauthorized", null);
        }

        return new ApiResponse<PaymentDto>(true, null, payment.ToDto());
    }

    public async Task<ApiResponse<PaymentDto>> GetByBookingIdAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByBookingIdAsync(bookingId, cancellationToken);
        if (payment == null)
        {
            return new ApiResponse<PaymentDto>(false, "Payment not found", null);
        }

        if (payment.UserId != userId)
        {
            return new ApiResponse<PaymentDto>(false, "Unauthorized", null);
        }

        return new ApiResponse<PaymentDto>(true, null, payment.ToDto());
    }

    public async Task<ApiResponse<PaymentResultDto>> ProcessPaymentAsync(Guid userId, CreatePaymentDto dto, CancellationToken cancellationToken = default)
    {
        // Get booking
        var booking = await _unitOfWork.Bookings.GetByIdAsync(dto.BookingId, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<PaymentResultDto>(false, "Booking not found", null);
        }

        if (booking.UserId != userId)
        {
            return new ApiResponse<PaymentResultDto>(false, "Unauthorized", null);
        }

        if (booking.Status != BookingStatus.AwaitingPayment)
        {
            return new ApiResponse<PaymentResultDto>(false, "Booking must be approved by the owner before payment", null);
        }

        // Check if payment already exists
        var existingPayment = await _unitOfWork.Payments.GetByBookingIdAsync(dto.BookingId, cancellationToken);
        if (existingPayment != null && existingPayment.Status == PaymentStatus.Completed)
        {
            return new ApiResponse<PaymentResultDto>(false, "Payment already completed", null);
        }

        // Process payment through gateway
        var paymentRequest = new PaymentRequest
        {
            BookingId = dto.BookingId,
            UserId = userId,
            Amount = booking.TotalAmount,
            Currency = "INR",
            PaymentMethod = dto.PaymentMethod,
            Description = $"Parking booking: {booking.BookingReference}"
        };

        var result = await _paymentService.ProcessPaymentAsync(paymentRequest, cancellationToken);

        // Create or update payment record
        var payment = existingPayment ?? new Payment
        {
            BookingId = dto.BookingId,
            UserId = userId,
            Amount = booking.TotalAmount,
            Currency = "INR",
            PaymentMethod = dto.PaymentMethod
        };

        payment.Status = result.Status;
        payment.TransactionId = result.TransactionId;
        payment.PaymentGatewayReference = result.PaymentGatewayReference;
        payment.PaymentGateway = "MockGateway";
        payment.ReceiptUrl = result.ReceiptUrl;

        if (result.Success)
        {
            payment.PaidAt = DateTime.UtcNow;
            payment.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            
            // Now set booking to Confirmed after successful payment
            booking.Status = BookingStatus.Confirmed;
            _unitOfWork.Bookings.Update(booking);
        }
        else
        {
            payment.FailureReason = result.ErrorMessage;
        }

        if (existingPayment == null)
        {
            await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        }
        else
        {
            _unitOfWork.Payments.Update(payment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify parking owner when payment is successful
        if (result.Success && booking.ParkingSpace?.OwnerId != null)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
            await _notificationService.NotifyUserAsync(
                booking.ParkingSpace.OwnerId,
                new NotificationDto(
                    NotificationTypes.PaymentCompleted,
                    "Payment Received",
                    $"{user?.FirstName ?? "A member"} has completed payment for booking {booking.BookingReference}",
                    new { BookingId = dto.BookingId, BookingReference = booking.BookingReference, Amount = booking.TotalAmount }
                ),
                cancellationToken);
        }

        var resultDto = new PaymentResultDto(
            result.Success,
            result.TransactionId,
            result.Status,
            result.Success ? "Payment successful" : result.ErrorMessage,
            result.ReceiptUrl
        );

        return new ApiResponse<PaymentResultDto>(result.Success, null, resultDto);
    }

    public async Task<ApiResponse<RefundResultDto>> ProcessRefundAsync(Guid userId, RefundRequestDto dto, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(dto.PaymentId, cancellationToken);
        if (payment == null)
        {
            return new ApiResponse<RefundResultDto>(false, "Payment not found", null);
        }

        if (payment.UserId != userId)
        {
            return new ApiResponse<RefundResultDto>(false, "Unauthorized", null);
        }

        if (payment.Status != PaymentStatus.Completed)
        {
            return new ApiResponse<RefundResultDto>(false, "Cannot refund a non-completed payment", null);
        }

        // Process refund
        var refundRequest = new RefundRequest
        {
            PaymentId = dto.PaymentId,
            Amount = dto.Amount,
            Reason = dto.Reason
        };

        var result = await _paymentService.ProcessRefundAsync(refundRequest, cancellationToken);

        if (result.Success)
        {
            payment.Status = dto.Amount >= payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartialRefund;
            payment.RefundAmount = (payment.RefundAmount ?? 0) + result.RefundedAmount;
            payment.RefundReason = dto.Reason;
            payment.RefundTransactionId = result.RefundTransactionId;
            payment.RefundedAt = DateTime.UtcNow;

            _unitOfWork.Payments.Update(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var resultDto = new RefundResultDto(
            result.Success,
            result.RefundTransactionId,
            result.RefundedAmount,
            result.Success ? "Refund processed successfully" : result.ErrorMessage
        );

        return new ApiResponse<RefundResultDto>(result.Success, null, resultDto);
    }
}

public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _unitOfWork;

    public ReviewService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<ReviewDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(id, cancellationToken);
        if (review == null)
        {
            return new ApiResponse<ReviewDto>(false, "Review not found", null);
        }

        return new ApiResponse<ReviewDto>(true, null, review.ToDto());
    }

    public async Task<ApiResponse<List<ReviewDto>>> GetByParkingSpaceAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default)
    {
        var reviews = await _unitOfWork.Reviews.GetByParkingSpaceIdAsync(parkingSpaceId, cancellationToken);
        var dtos = reviews.Select(r => r.ToDto()).ToList();

        return new ApiResponse<List<ReviewDto>>(true, null, dtos);
    }

    public async Task<ApiResponse<ReviewDto>> CreateAsync(Guid userId, CreateReviewDto dto, CancellationToken cancellationToken = default)
    {
        // Verify parking space exists
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(dto.ParkingSpaceId, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<ReviewDto>(false, "Parking space not found", null);
        }

        // Check if user has a completed booking (optional but recommended)
        if (dto.BookingId.HasValue)
        {
            var booking = await _unitOfWork.Bookings.GetByIdAsync(dto.BookingId.Value, cancellationToken);
            if (booking == null || booking.UserId != userId || booking.Status != BookingStatus.Completed)
            {
                return new ApiResponse<ReviewDto>(false, "Invalid booking reference", null);
            }
        }

        // Check for existing review
        var existingReview = await _unitOfWork.Reviews.FirstOrDefaultAsync(
            r => r.UserId == userId && r.ParkingSpaceId == dto.ParkingSpaceId, cancellationToken);
        
        if (existingReview != null)
        {
            return new ApiResponse<ReviewDto>(false, "You have already reviewed this parking space", null);
        }

        var review = new Review
        {
            UserId = userId,
            ParkingSpaceId = dto.ParkingSpaceId,
            BookingId = dto.BookingId,
            Rating = dto.Rating,
            Title = dto.Title?.Trim(),
            Comment = dto.Comment?.Trim()
        };

        await _unitOfWork.Reviews.AddAsync(review, cancellationToken);

        // Update parking space rating
        parking.TotalReviews++;
        var newAverage = await _unitOfWork.Reviews.GetAverageRatingAsync(dto.ParkingSpaceId, cancellationToken);
        // Include the new review in calculation
        parking.AverageRating = ((newAverage * (parking.TotalReviews - 1)) + dto.Rating) / parking.TotalReviews;
        _unitOfWork.ParkingSpaces.Update(parking);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<ReviewDto>(true, "Review submitted", review.ToDto());
    }

    public async Task<ApiResponse<ReviewDto>> UpdateAsync(Guid id, Guid userId, UpdateReviewDto dto, CancellationToken cancellationToken = default)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(id, cancellationToken);
        if (review == null)
        {
            return new ApiResponse<ReviewDto>(false, "Review not found", null);
        }

        if (review.UserId != userId)
        {
            return new ApiResponse<ReviewDto>(false, "Unauthorized", null);
        }

        var oldRating = review.Rating;

        if (dto.Rating.HasValue) review.Rating = dto.Rating.Value;
        if (dto.Title != null) review.Title = dto.Title.Trim();
        if (dto.Comment != null) review.Comment = dto.Comment.Trim();

        _unitOfWork.Reviews.Update(review);

        // Update parking space rating if rating changed
        if (dto.Rating.HasValue && dto.Rating.Value != oldRating)
        {
            var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(review.ParkingSpaceId, cancellationToken);
            if (parking != null && parking.TotalReviews > 0)
            {
                // Adjust average: remove old rating, add new rating
                var currentTotal = parking.AverageRating * parking.TotalReviews;
                parking.AverageRating = (currentTotal - oldRating + dto.Rating.Value) / parking.TotalReviews;
                _unitOfWork.ParkingSpaces.Update(parking);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<ReviewDto>(true, "Review updated", review.ToDto());
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(id, cancellationToken);
        if (review == null)
        {
            return new ApiResponse<bool>(false, "Review not found", false);
        }

        if (review.UserId != userId)
        {
            return new ApiResponse<bool>(false, "Unauthorized", false);
        }

        var parkingSpaceId = review.ParkingSpaceId;
        var rating = review.Rating;

        _unitOfWork.Reviews.Remove(review);

        // Update parking space rating
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking != null && parking.TotalReviews > 0)
        {
            if (parking.TotalReviews == 1)
            {
                parking.AverageRating = 0;
                parking.TotalReviews = 0;
            }
            else
            {
                var currentTotal = parking.AverageRating * parking.TotalReviews;
                parking.TotalReviews--;
                parking.AverageRating = (currentTotal - rating) / parking.TotalReviews;
            }
            _unitOfWork.ParkingSpaces.Update(parking);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<bool>(true, "Review deleted", true);
    }

    public async Task<ApiResponse<ReviewDto>> AddOwnerResponseAsync(Guid reviewId, Guid ownerId, OwnerResponseDto dto, CancellationToken cancellationToken = default)
    {
        var review = await _unitOfWork.Reviews.GetByIdAsync(reviewId, cancellationToken);
        if (review == null)
        {
            return new ApiResponse<ReviewDto>(false, "Review not found", null);
        }

        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(review.ParkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != ownerId)
        {
            return new ApiResponse<ReviewDto>(false, "Unauthorized", null);
        }

        review.OwnerResponse = dto.Response.Trim();
        review.OwnerResponseAt = DateTime.UtcNow;

        _unitOfWork.Reviews.Update(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<ReviewDto>(true, "Response added", review.ToDto());
    }
}

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<VendorDashboardDto>> GetVendorDashboardAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        var parkingSpaces = (await _unitOfWork.ParkingSpaces.GetByOwnerIdAsync(vendorId, cancellationToken)).ToList();
        var parkingSpaceIds = parkingSpaces.Select(p => p.Id).ToList();

        // Get all bookings for vendor's parking spaces
        var allBookings = new List<Booking>();
        foreach (var id in parkingSpaceIds)
        {
            var bookings = await _unitOfWork.Bookings.GetByParkingSpaceIdAsync(id, cancellationToken);
            allBookings.AddRange(bookings);
        }

        var now = DateTime.UtcNow;
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
        var startOfMonth = new DateTime(now.Year, now.Month, 1);

        // Calculate earnings from completed payments
        var completedBookings = allBookings.Where(b => b.Status == BookingStatus.Completed && b.Payment?.Status == PaymentStatus.Completed).ToList();
        var totalEarnings = completedBookings.Sum(b => b.TotalAmount);
        var monthlyEarnings = completedBookings.Where(b => b.CheckOutTime >= startOfMonth).Sum(b => b.TotalAmount);
        var weeklyEarnings = completedBookings.Where(b => b.CheckOutTime >= startOfWeek).Sum(b => b.TotalAmount);

        // Earnings chart (last 7 days)
        var earningsChart = Enumerable.Range(0, 7)
            .Select(i => now.Date.AddDays(-6 + i))
            .Select(date => new EarningsChartDataDto(
                date.ToString("ddd"),
                completedBookings.Where(b => b.CheckOutTime?.Date == date).Sum(b => b.TotalAmount)
            ))
            .ToList();

        // Recent bookings
        var recentBookings = allBookings
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .Select(b => b.ToDto())
            .ToList();

        var dashboard = new VendorDashboardDto(
            TotalParkingSpaces: parkingSpaces.Count,
            ActiveParkingSpaces: parkingSpaces.Count(p => p.IsActive),
            TotalBookings: allBookings.Count,
            ActiveBookings: allBookings.Count(b => b.Status == BookingStatus.InProgress),
            PendingBookings: allBookings.Count(b => b.Status == BookingStatus.Pending),
            CompletedBookings: allBookings.Count(b => b.Status == BookingStatus.Completed),
            TotalEarnings: totalEarnings,
            MonthlyEarnings: monthlyEarnings,
            WeeklyEarnings: weeklyEarnings,
            AverageRating: parkingSpaces.Any() ? parkingSpaces.Average(p => p.AverageRating) : 0,
            TotalReviews: parkingSpaces.Sum(p => p.TotalReviews),
            RecentBookings: recentBookings,
            EarningsChart: earningsChart
        );

        return new ApiResponse<VendorDashboardDto>(true, null, dashboard);
    }

    public async Task<ApiResponse<MemberDashboardDto>> GetMemberDashboardAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var bookings = (await _unitOfWork.Bookings.GetByUserIdAsync(memberId, cancellationToken)).ToList();
        var now = DateTime.UtcNow;

        var upcomingBookings = bookings
            .Where(b => b.StartDateTime > now && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending))
            .OrderBy(b => b.StartDateTime)
            .Take(5)
            .Select(b => b.ToDto())
            .ToList();

        var recentBookings = bookings
            .Where(b => b.Status == BookingStatus.Completed)
            .OrderByDescending(b => b.CheckOutTime)
            .Take(5)
            .Select(b => b.ToDto())
            .ToList();

        var totalSpent = bookings
            .Where(b => b.Payment?.Status == PaymentStatus.Completed)
            .Sum(b => b.TotalAmount);

        var dashboard = new MemberDashboardDto(
            TotalBookings: bookings.Count,
            ActiveBookings: bookings.Count(b => b.Status == BookingStatus.InProgress || b.Status == BookingStatus.Confirmed),
            CompletedBookings: bookings.Count(b => b.Status == BookingStatus.Completed),
            TotalSpent: totalSpent,
            UpcomingBookings: upcomingBookings,
            RecentBookings: recentBookings
        );

        return new ApiResponse<MemberDashboardDto>(true, null, dashboard);
    }
}
