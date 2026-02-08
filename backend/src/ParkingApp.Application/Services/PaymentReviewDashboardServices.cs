using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using ParkingApp.BuildingBlocks.Logging;

namespace ParkingApp.Application.Services;

public class PaymentAppService : IPaymentAppService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PaymentAppService> _logger;

    public PaymentAppService(IUnitOfWork unitOfWork, IPaymentService paymentService, INotificationService notificationService, ILogger<PaymentAppService> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ApiResponse<PaymentDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(id, cancellationToken);
        if (payment == null)
        {
            _logger.LogEntityNotFound<Payment>(id);
            return new ApiResponse<PaymentDto>(false, "Payment not found", null);
        }

        if (payment.Booking.UserId != userId)
        {
            _logger.LogUnauthorizedAccess(userId, $"Payment:{id}");
            return new ApiResponse<PaymentDto>(false, "Unauthorized", null);
        }

        return new ApiResponse<PaymentDto>(true, null, payment.ToDto());
    }

    public async Task<ApiResponse<PaymentDto>> GetByBookingIdAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByBookingIdAsync(bookingId, cancellationToken);
        if (payment == null)
        {
            _logger.LogEntityNotFound<Payment>(bookingId);
            return new ApiResponse<PaymentDto>(false, "Payment not found", null);
        }

        if (payment.UserId != userId)
        {
            _logger.LogWarning("Unauthorized access attempt to payment for booking {BookingId} by user {UserId}.", bookingId, userId);
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
            _logger.LogWarning("Booking with ID {BookingId} not found for payment processing by user {UserId}.", dto.BookingId, userId);
            return new ApiResponse<PaymentResultDto>(false, "Booking not found", null);
        }

        if (booking.UserId != userId)
        {
            _logger.LogWarning("Unauthorized attempt to process payment for booking {BookingId} by user {UserId}.", dto.BookingId, userId);
            return new ApiResponse<PaymentResultDto>(false, "Unauthorized", null);
        }

        if (booking.Status != BookingStatus.AwaitingPayment)
        {
            _logger.LogWarning("Payment attempted for booking {BookingId} with status {BookingStatus} (expected AwaitingPayment).", dto.BookingId, booking.Status);
            return new ApiResponse<PaymentResultDto>(false, "Booking must be approved by the owner before payment", null);
        }

        // Check if payment already exists
        var existingPayment = await _unitOfWork.Payments.GetByBookingIdAsync(dto.BookingId, cancellationToken);
        if (existingPayment != null && existingPayment.Status == PaymentStatus.Completed)
        {
            _logger.LogInformation("Payment already completed for booking {BookingId}.", dto.BookingId);
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

        _logger.LogInformation("Initiating payment processing for booking {BookingId} with amount {Amount}.", dto.BookingId, booking.TotalAmount);
        var result = await _paymentService.ProcessPaymentAsync(paymentRequest, cancellationToken);
        _logger.LogInformation("Payment gateway result for booking {BookingId}: Success={Success}, TransactionId={TransactionId}, Status={Status}.",
            dto.BookingId, result.Success, result.TransactionId, result.Status);

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
            _logger.LogInformation("Payment {PaymentId} completed for booking {BookingReference}, amount: {Amount:C}", 
                payment.Id, booking.BookingReference, booking.TotalAmount);
        }
        else
        {
            payment.FailureReason = result.ErrorMessage;
            _logger.LogError("Payment failed for booking {BookingId}. Reason: {ErrorMessage}", dto.BookingId, result.ErrorMessage);
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

        // Send notification
        if (result.Success)
        {
            await NotifyOwnerOfPaymentAsync(booking, userId, cancellationToken);
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

    public async Task<ApiResponse<string>> CreateRazorpayOrderAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(bookingId, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<string>(false, "Booking not found", null);
        }

        if (booking.UserId != userId)
        {
            return new ApiResponse<string>(false, "Unauthorized", null);
        }

        if (booking.Status != BookingStatus.AwaitingPayment)
        {
            return new ApiResponse<string>(false, "Booking is not awaiting payment", null);
        }

        try
        {
            var orderId = await _paymentService.CreateOrderAsync(booking.TotalAmount, "INR", null, cancellationToken);
            return new ApiResponse<string>(true, null, orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Razorpay order for booking {BookingId}", bookingId);
            return new ApiResponse<string>(false, "Failed to create payment order", null);
        }
    }

    public async Task<ApiResponse<PaymentResultDto>> ProcessRazorpayPaymentAsync(Guid userId, VerifyPaymentDto dto, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdAsync(dto.BookingId, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<PaymentResultDto>(false, "Booking not found", null);
        }

        if (booking.UserId != userId)
        {
            return new ApiResponse<PaymentResultDto>(false, "Unauthorized", null);
        }

        // Check if already paid
        var existingPayment = await _unitOfWork.Payments.GetByBookingIdAsync(dto.BookingId, cancellationToken);
        if (existingPayment != null && existingPayment.Status == PaymentStatus.Completed)
        {
            return new ApiResponse<PaymentResultDto>(true, "Payment already completed", new PaymentResultDto(
                true,
                existingPayment.TransactionId,
                PaymentStatus.Completed,
                "Payment already completed",
                existingPayment.ReceiptUrl
            ));
        }

        // Verify Signature
        var isValid = await _paymentService.VerifyPaymentSignatureAsync(dto.RazorpayPaymentId, dto.RazorpayOrderId, dto.RazorpaySignature, cancellationToken);
        if (!isValid)
        {
            _logger.LogWarning("Invalid Razorpay signature for booking {BookingId}", dto.BookingId);
            return new ApiResponse<PaymentResultDto>(false, "Invalid payment signature", null);
        }

        // Create Payment Record
        var payment = existingPayment ?? new Payment
        {
            BookingId = dto.BookingId,
            UserId = userId,
            Amount = booking.TotalAmount,
            Currency = "INR",
            PaymentMethod = PaymentMethod.CreditCard // Default since we don't know method from Razorpay callback easily without API call
        };

        payment.Status = PaymentStatus.Completed;
        payment.TransactionId = dto.RazorpayPaymentId;
        payment.PaymentGatewayReference = dto.RazorpayOrderId;
        payment.PaymentGateway = "Razorpay";
        payment.PaidAt = DateTime.UtcNow;
        payment.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        // Update Booking
        booking.Status = BookingStatus.Confirmed;
        _unitOfWork.Bookings.Update(booking);

        if (existingPayment == null)
        {
            await _unitOfWork.Payments.AddAsync(payment, cancellationToken);
        }
        else
        {
            _unitOfWork.Payments.Update(payment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send notification
        await NotifyOwnerOfPaymentAsync(booking, userId, cancellationToken);

        return new ApiResponse<PaymentResultDto>(true, "Payment verified successfully", new PaymentResultDto(
            true,
            payment.TransactionId,
            PaymentStatus.Completed,
            "Payment verified successfully",
            null
        ));
    }

    private async Task NotifyOwnerOfPaymentAsync(Booking booking, Guid payerId, CancellationToken cancellationToken)
    {
        try
        {
            var parkingSpace = booking.ParkingSpace;
            if (parkingSpace == null)
            {
                parkingSpace = await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);
            }

            if (parkingSpace == null) return;

            var payer = await _unitOfWork.Users.GetByIdAsync(payerId, cancellationToken);
            var payerName = payer?.FirstName ?? "A user";

            await _notificationService.NotifyUserAsync(
                parkingSpace.OwnerId,
                new NotificationDto(
                    "payment.completed",
                    "Payment Received",
                    $"{payerName} has completed payment for booking {booking.BookingReference}",
                    new { BookingId = booking.Id, BookingReference = booking.BookingReference, Amount = booking.TotalAmount }
                ),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment notification for booking {BookingId}", booking.Id);
        }
    }

    public async Task<ApiResponse<RefundResultDto>> ProcessRefundAsync(Guid userId, RefundRequestDto dto, CancellationToken cancellationToken = default)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(dto.PaymentId, cancellationToken);
        if (payment == null)
        {
            _logger.LogWarning("Refund requested for non-existent payment ID {PaymentId} by user {UserId}.", dto.PaymentId, userId);
            return new ApiResponse<RefundResultDto>(false, "Payment not found", null);
        }

        if (payment.UserId != userId)
        {
            _logger.LogWarning("Unauthorized refund attempt for payment {PaymentId} by user {UserId}.", dto.PaymentId, userId);
            return new ApiResponse<RefundResultDto>(false, "Unauthorized", null);
        }

        if (payment.Status != PaymentStatus.Completed)
        {
            _logger.LogWarning("Refund attempted for payment {PaymentId} with status {PaymentStatus} (expected Completed).", dto.PaymentId, payment.Status);
            return new ApiResponse<RefundResultDto>(false, "Cannot refund a non-completed payment", null);
        }

        // Process refund
        var refundRequest = new RefundRequest
        {
            PaymentId = dto.PaymentId,
            Amount = dto.Amount,
            Reason = dto.Reason
        };

        _logger.LogInformation("Initiating refund processing for payment {PaymentId} with amount {Amount}.", dto.PaymentId, dto.Amount);
        var result = await _paymentService.ProcessRefundAsync(refundRequest, cancellationToken);
        _logger.LogInformation("Refund gateway result for payment {PaymentId}: Success={Success}, RefundTransactionId={RefundTransactionId}.",
            dto.PaymentId, result.Success, result.RefundTransactionId);

        if (result.Success)
        {
            payment.Status = dto.Amount >= payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartialRefund;
            payment.RefundAmount = (payment.RefundAmount ?? 0) + result.RefundedAmount;
            payment.RefundReason = dto.Reason;
            payment.RefundTransactionId = result.RefundTransactionId;
            payment.RefundedAt = DateTime.UtcNow;

            _unitOfWork.Payments.Update(payment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Refund processed successfully for payment {PaymentId}. Refunded amount: {RefundedAmount:C}.", payment.Id, result.RefundedAmount);
        }
        else
        {
            _logger.LogError("Refund failed for payment {PaymentId}. Reason: {ErrorMessage}", dto.PaymentId, result.ErrorMessage);
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
    private readonly ICacheService _cache;
    private readonly ILogger<ReviewService> _logger;
    private static readonly TimeSpan ReviewCacheDuration = TimeSpan.FromMinutes(10);

    public ReviewService(IUnitOfWork unitOfWork, ICacheService cache, ILogger<ReviewService> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
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
        // Try cache first
        var cacheKey = $"reviews:parking:{parkingSpaceId}";
        var cached = await _cache.GetAsync<List<ReviewDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return new ApiResponse<List<ReviewDto>>(true, null, cached);
        }

        // Cache miss - fetch from DB
        var reviews = await _unitOfWork.Reviews.GetByParkingSpaceIdAsync(parkingSpaceId, cancellationToken);
        var dtos = reviews.Select(r => r.ToDto()).ToList();

        // Cache the result
        await _cache.SetAsync(cacheKey, dtos, ReviewCacheDuration, cancellationToken);

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

        // Invalidate caches (reviews and parking space due to rating update)
        await _cache.RemoveAsync($"reviews:parking:{dto.ParkingSpaceId}", cancellationToken);
        await _cache.RemoveAsync($"parking:{dto.ParkingSpaceId}", cancellationToken);
        
        _logger.LogInformation("Review submitted for parking {ParkingSpaceId} by user {UserId}, rating: {Rating}", 
            dto.ParkingSpaceId, userId, dto.Rating);

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

        // Invalidate caches
        await _cache.RemoveAsync($"reviews:parking:{review.ParkingSpaceId}", cancellationToken);
        if (dto.Rating.HasValue && dto.Rating.Value != oldRating)
        {
            // Rating changed - invalidate parking cache too
            await _cache.RemoveAsync($"parking:{review.ParkingSpaceId}", cancellationToken);
        }

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

        // Invalidate caches (reviews and parking due to rating update)
        await _cache.RemoveAsync($"reviews:parking:{parkingSpaceId}", cancellationToken);
        await _cache.RemoveAsync($"parking:{parkingSpaceId}", cancellationToken);

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

        // Invalidate review cache
        await _cache.RemoveAsync($"reviews:parking:{review.ParkingSpaceId}", cancellationToken);

        return new ApiResponse<ReviewDto>(true, "Response added", review.ToDto());
    }
}

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<DashboardService> _logger;
    private static readonly TimeSpan DashboardCacheDuration = TimeSpan.FromMinutes(5);

    public DashboardService(IUnitOfWork unitOfWork, ICacheService cache, ILogger<DashboardService> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<VendorDashboardDto>> GetVendorDashboardAsync(Guid vendorId, CancellationToken cancellationToken = default)
    {
        // Try cache first
        var cacheKey = $"dashboard:vendor:{vendorId}";
        var cached = await _cache.GetAsync<VendorDashboardDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for vendor dashboard: {VendorId}", vendorId);
            return new ApiResponse<VendorDashboardDto>(true, null, cached);
        }

        _logger.LogInformation("Generating vendor dashboard for vendor {VendorId}", vendorId);
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

        // Cache the result
        await _cache.SetAsync(cacheKey, dashboard, DashboardCacheDuration, cancellationToken);

        return new ApiResponse<VendorDashboardDto>(true, null, dashboard);
    }

    public async Task<ApiResponse<MemberDashboardDto>> GetMemberDashboardAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        // Try cache first
        var cacheKey = $"dashboard:member:{memberId}";
        var cached = await _cache.GetAsync<MemberDashboardDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return new ApiResponse<MemberDashboardDto>(true, null, cached);
        }

        // Cache miss - generate dashboard
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

        // Cache the result
        await _cache.SetAsync(cacheKey, dashboard, DashboardCacheDuration, cancellationToken);

        return new ApiResponse<MemberDashboardDto>(true, null, dashboard);
    }
}
