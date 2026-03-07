using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.BuildingBlocks.Extensions;

namespace ParkingApp.Application.CQRS.Commands.Bookings;

public class CreateBookingHandler : ICommandHandler<CreateBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public CreateBookingHandler(IUnitOfWork unitOfWork, INotificationCoordinator notificationCoordinator, IEmailService emailService, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _emailService = emailService;
        _cache = cache;
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

        if (command.SlotNumber.HasValue && (command.SlotNumber.Value < 1 || command.SlotNumber.Value > parking.TotalSpots))
        {
            return new ApiResponse<BookingDto>(false, $"Slot must be between 1 and {parking.TotalSpots}", null);
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

        if (command.SlotNumber.HasValue)
        {
            var isSelectedSlotBooked = await _unitOfWork.Bookings.AnyAsync(b =>
                b.ParkingSpaceId == command.ParkingSpaceId &&
                b.SlotNumber == command.SlotNumber.Value &&
                (b.Status == BookingStatus.Pending ||
                 b.Status == BookingStatus.AwaitingPayment ||
                 b.Status == BookingStatus.Confirmed ||
                 b.Status == BookingStatus.InProgress) &&
                b.StartDateTime < command.EndDateTime &&
                b.EndDateTime > command.StartDateTime,
                cancellationToken);

            if (isSelectedSlotBooked)
            {
                return new ApiResponse<BookingDto>(false, $"Slot {command.SlotNumber.Value} is already booked for the selected time", null);
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
            StartDateTime = command.StartDateTime.ToUtc(),
            EndDateTime = command.EndDateTime.ToUtc(),
            PricingType = command.PricingType,
            VehicleType = command.VehicleType,
            SlotNumber = command.SlotNumber,
            VehicleNumber = command.VehicleNumber?.Trim(),
            VehicleModel = command.VehicleModel?.Trim(),
            VehicleColor = command.VehicleColor?.Trim(),
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

        // Reload with navigation properties
        booking = await _unitOfWork.Bookings.GetByIdAsync(booking.Id, cancellationToken);

        // Notify parking owner of new booking request
        var memberName = booking?.User != null ? $"{booking.User.FirstName} {booking.User.LastName}" : "A member";
        await _notificationCoordinator.SendAsync(
            parking.OwnerId,
            new NotificationRequest(
                NotificationType.BookingRequest.ToString(),
                "New Booking Request",
                $"New booking request from {memberName} for {parking.Title}",
                NotificationChannels.InApp,
                new Dictionary<string, string> { { "BookingId", booking!.Id.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty } }
            ),
            cancellationToken);

        // Send Email to Owner
        if (parking.Owner?.Email != null)
        {
            await _emailService.SendEmailAsync(
                parking.Owner.Email,
                $"New Booking Request: {booking.BookingReference}",
                $"<p>Hello {parking.Owner.FirstName},</p><p>You have a new booking request from {memberName} for <strong>{parking.Title}</strong>.</p><p>Please log in to your dashboard to approve or reject it.</p>"
            );
        }

        if (booking.User?.Email != null)
        {
             await _emailService.SendEmailAsync(
                booking.User.Email,
                $"Booking Requested: {booking.BookingReference}",
                $"<p>Hello {booking.User.FirstName},</p><p>Your booking request for <strong>{parking.Title}</strong> has been sent.</p><p>You will be notified once the owner approves it.</p>"
            );
        }

        // Invalidate caches
        await _cache.RemoveAsync($"parking:{command.ParkingSpaceId}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);

        return new ApiResponse<BookingDto>(true, "Booking created successfully", booking.ToDto());
    }
}

public class CancelBookingHandler : ICommandHandler<CancelBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public CancelBookingHandler(IUnitOfWork unitOfWork, INotificationCoordinator notificationCoordinator, IEmailService emailService, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _emailService = emailService;
        _cache = cache;
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

            // Notify the other party about cancellation
            var recipientId = booking.UserId == command.UserId ? booking.ParkingSpace?.OwnerId : booking.UserId;
            if (recipientId.HasValue)
            {
                await _notificationCoordinator.SendAsync(
                    recipientId.Value,
                    new NotificationRequest(
                        NotificationType.BookingRejected.ToString(),
                        "Booking Cancelled",
                        $"Booking {booking.BookingReference} has been cancelled",
                        NotificationChannels.InApp,
                        new Dictionary<string, string> { { "BookingId", command.BookingId.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty } }
                    ),
                    cancellationToken);
            }

            // Send Email to the cancelled party
            var owner = booking.ParkingSpace?.Owner;
            var user = booking.User;

            if (owner?.Email != null)
            {
                 await _emailService.SendEmailAsync(
                    owner.Email,
                    $"Booking Cancelled: {booking.BookingReference}",
                    $"<p>Hello {owner.FirstName},</p><p>The booking {booking.BookingReference} for <strong>{booking.ParkingSpace?.Title}</strong> has been cancelled.</p>"
                );
            }
            if (user?.Email != null)
            {
                 await _emailService.SendEmailAsync(
                    user.Email,
                    $"Booking Cancelled: {booking.BookingReference}",
                    $"<p>Hello {user.FirstName},</p><p>The booking {booking.BookingReference} for <strong>{booking.ParkingSpace?.Title}</strong> has been cancelled.</p>"
                );
            }

            // Invalidate caches
            if (booking.ParkingSpaceId != Guid.Empty)
            {
                await _cache.RemoveAsync($"parking:{booking.ParkingSpaceId}", cancellationToken);
                await _cache.RemoveByPatternAsync("search:*", cancellationToken);
            }

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
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public ApproveBookingHandler(IUnitOfWork unitOfWork, INotificationCoordinator notificationCoordinator, IEmailService emailService, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _emailService = emailService;
        _cache = cache;
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

            // Notify member that their booking was approved
            await _notificationCoordinator.SendAsync(
                booking.UserId,
                new NotificationRequest(
                    NotificationType.BookingConfirmed.ToString(),
                    "Booking Approved!",
                    $"Your booking for {booking.ParkingSpace?.Title} has been approved. Please complete payment.",
                    NotificationChannels.InApp,
                    new Dictionary<string, string> { { "BookingId", command.BookingId.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty } }
                ),
                cancellationToken);

            // Notify vendor (owner) so UI count refreshes
            await _notificationCoordinator.SendAsync(
                booking.ParkingSpace.OwnerId,
                new NotificationRequest(
                    NotificationType.BookingConfirmed.ToString(),
                    "Booking Approved",
                    $"You have approved booking {booking.BookingReference}",
                    NotificationChannels.InApp,
                    new Dictionary<string, string> { { "BookingId", command.BookingId.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty }, { "Silent", "true" } }
                ),
                cancellationToken);

            // Send Email to Member
            if (booking.User?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    booking.User.Email,
                    $"Booking Approved: {booking.BookingReference}",
                    $"<p>Hello {booking.User.FirstName},</p><p>Great news! Your booking for <strong>{booking.ParkingSpace?.Title}</strong> has been approved.</p><p>Please log in and complete your payment to confirm the reservation.</p>"
                );
            }

            // Invalidate caches
            await _cache.RemoveAsync($"parking:{booking.ParkingSpaceId}", cancellationToken);
            await _cache.RemoveByPatternAsync("search:*", cancellationToken);

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
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public RejectBookingHandler(IUnitOfWork unitOfWork, INotificationCoordinator notificationCoordinator, IEmailService emailService, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _emailService = emailService;
        _cache = cache;
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

            // Notify member that their booking was rejected
            await _notificationCoordinator.SendAsync(
                booking.UserId,
                new NotificationRequest(
                    NotificationType.BookingRejected.ToString(),
                    "Booking Rejected",
                    $"Your booking for {booking.ParkingSpace?.Title} was rejected. Reason: {booking.CancellationReason}",
                    NotificationChannels.InApp,
                    new Dictionary<string, string> { { "BookingId", command.BookingId.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty }, { "Reason", booking.CancellationReason ?? string.Empty } }
                ),
                cancellationToken);

            // Notify vendor (owner) so UI count refreshes
            await _notificationCoordinator.SendAsync(
                booking.ParkingSpace.OwnerId,
                new NotificationRequest(
                    NotificationType.BookingRejected.ToString(),
                    "Booking Rejected",
                    $"You have rejected booking {booking.BookingReference}",
                    NotificationChannels.InApp,
                    new Dictionary<string, string> { { "BookingId", command.BookingId.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty }, { "Silent", "true" } }
                ),
                cancellationToken);

            // Send Email to Member
            if (booking.User?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    booking.User.Email,
                    $"Booking Rejected: {booking.BookingReference}",
                    $"<p>Hello {booking.User.FirstName},</p><p>We're sorry, but your booking for <strong>{booking.ParkingSpace?.Title}</strong> was rejected.</p><p><strong>Reason:</strong> {booking.CancellationReason}</p>"
                );
            }

            // Invalidate caches
            await _cache.RemoveAsync($"parking:{booking.ParkingSpaceId}", cancellationToken);
            await _cache.RemoveByPatternAsync("search:*", cancellationToken);

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
    private readonly INotificationCoordinator _notificationCoordinator;

    public CheckInHandler(IUnitOfWork unitOfWork, INotificationCoordinator notificationCoordinator)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
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

            // Notify owner of check-in
            if (booking.ParkingSpace?.OwnerId != null)
            {
                await _notificationCoordinator.SendAsync(
                    booking.ParkingSpace.OwnerId,
                    new NotificationRequest(
                        NotificationType.SystemAlert.ToString(),
                        "Guest Checked In",
                        $"{booking.User?.FirstName} has checked in at {booking.ParkingSpace.Title}",
                        NotificationChannels.InApp,
                        new Dictionary<string, string> { { "BookingId", command.BookingId.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty } }
                    ),
                    cancellationToken);
            }

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

public class RequestExtensionHandler : ICommandHandler<RequestExtensionCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public RequestExtensionHandler(
        IUnitOfWork unitOfWork,
        INotificationCoordinator notificationCoordinator,
        IEmailService emailService,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _emailService = emailService;
        _cache = cache;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(RequestExtensionCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);

        if (booking.UserId != command.UserId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        var newEndDateTime = command.NewEndDateTime.ToUtc();
        if (newEndDateTime <= booking.EndDateTime)
            return new ApiResponse<BookingDto>(false, "Extension end date/time must be greater than current booking end date/time", null);
        // Check availability for the extension period (current end → new end), excluding this booking
        var parking = booking.ParkingSpace
            ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);

        if (parking == null)
            return new ApiResponse<BookingDto>(false, "Parking space not found", null);

        var hasOverlap = await _unitOfWork.Bookings.HasOverlappingBookingAsync(
            booking.ParkingSpaceId, booking.EndDateTime, newEndDateTime, booking.Id, cancellationToken);

        if (hasOverlap)
        {
            var activeCount = await _unitOfWork.Bookings.GetActiveBookingsCountAsync(
                booking.ParkingSpaceId, booking.EndDateTime, newEndDateTime, cancellationToken);
            if (activeCount >= parking.TotalSpots)
                return new ApiResponse<BookingDto>(false, "Parking spot is not available for the extended period", null);
        }

        // Calculate extra cost (only for the extension window)
        decimal extraBaseAmount = booking.PricingType switch
        {
            PricingType.Hourly => parking.HourlyRate * (decimal)Math.Ceiling((newEndDateTime - booking.EndDateTime).TotalHours),
            PricingType.Daily  => parking.DailyRate  * (decimal)Math.Ceiling((newEndDateTime - booking.EndDateTime).TotalDays),
            PricingType.Weekly => parking.WeeklyRate  * (decimal)Math.Ceiling((newEndDateTime - booking.EndDateTime).TotalDays / 7),
            PricingType.Monthly=> parking.MonthlyRate * (decimal)Math.Ceiling((newEndDateTime - booking.EndDateTime).TotalDays / 30),
            _                  => parking.HourlyRate  * (decimal)Math.Ceiling((newEndDateTime - booking.EndDateTime).TotalHours)
        };
        var extraTax = Math.Round(extraBaseAmount * 0.18m, 2);
        var extraFee = Math.Round(extraBaseAmount * 0.05m, 2);
        var totalExtra = extraBaseAmount + extraTax + extraFee;

        try
        {
            booking.RequestExtension(newEndDateTime, totalExtra);
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse<BookingDto>(false, ex.Message, null);
        }

        // Notify vendor
        var memberName = booking.User != null ? $"{booking.User.FirstName} {booking.User.LastName}" : "A member";
        await _notificationCoordinator.SendAsync(
            parking.OwnerId,
            new NotificationRequest(
                NotificationType.BookingRequest.ToString(),
                "Extension Request",
                $"{memberName} has requested an extension for booking {booking.BookingReference} at {parking.Title}",
                NotificationChannels.InApp,
                new Dictionary<string, string>
                {
                    { "BookingId", booking.Id.ToString() },
                    { "BookingReference", booking.BookingReference ?? string.Empty },
                    { "Type", "Extension" }
                }),
            cancellationToken);

        if (parking.Owner?.Email != null)
        {
            await _emailService.SendEmailAsync(
                parking.Owner.Email,
                $"Extension Request: {booking.BookingReference}",
                $"<p>Hello {parking.Owner.FirstName},</p>" +
                $"<p>{memberName} has requested to extend booking <strong>{booking.BookingReference}</strong> " +
                $"at <strong>{parking.Title}</strong> to {newEndDateTime:f}.</p>" +
                $"<p>Additional charge: <strong>₹{totalExtra:F2}</strong>.</p>" +
                "<p>Please log in to approve or reject the request.</p>");
        }

        // Notify user
        if (booking.User?.Email != null)
        {
            await _emailService.SendEmailAsync(
                booking.User.Email,
                $"Extension Requested: {booking.BookingReference}",
                $"<p>Hello {booking.User.FirstName},</p>" +
                $"<p>Your extension request for <strong>{parking.Title}</strong> has been sent to the owner.</p>" +
                "<p>You will be notified once the owner responds.</p>");
        }

        await _cache.RemoveAsync($"parking:{booking.ParkingSpaceId}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);

        return new ApiResponse<BookingDto>(true,
            "Extension request submitted. Awaiting owner approval.", booking.ToDto());
    }
}

public class ApproveExtensionHandler : ICommandHandler<ApproveExtensionCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public ApproveExtensionHandler(
        IUnitOfWork unitOfWork,
        INotificationCoordinator notificationCoordinator,
        IEmailService emailService,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _emailService = emailService;
        _cache = cache;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(ApproveExtensionCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);

        if (booking.ParkingSpace?.OwnerId != command.VendorId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        try
        {
            booking.ApproveExtension();
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse<BookingDto>(false, ex.Message, null);
        }

        // Notify user to pay
        await _notificationCoordinator.SendAsync(
            booking.UserId,
            new NotificationRequest(
                NotificationType.BookingConfirmed.ToString(),
                "Extension Approved!",
                $"Your extension request for {booking.ParkingSpace?.Title} was approved. Please complete the payment.",
                NotificationChannels.InApp,
                new Dictionary<string, string>
                {
                    { "BookingId", booking.Id.ToString() },
                    { "BookingReference", booking.BookingReference ?? string.Empty },
                    { "Type", "Extension" }
                }),
            cancellationToken);

        // Notify vendor so UI count refreshes
        await _notificationCoordinator.SendAsync(
            booking.ParkingSpace.OwnerId,
            new NotificationRequest(
                NotificationType.BookingConfirmed.ToString(),
                "Extension Approved",
                $"You have approved the extension for {booking.BookingReference}",
                NotificationChannels.InApp,
                new Dictionary<string, string>
                {
                    { "BookingId", booking.Id.ToString() },
                    { "BookingReference", booking.BookingReference ?? string.Empty },
                    { "Type", "Extension" },
                    { "Silent", "true" }
                }),
            cancellationToken);

        if (booking.User?.Email != null)
        {
            await _emailService.SendEmailAsync(
                booking.User.Email,
                $"Extension Approved: {booking.BookingReference}",
                $"<p>Hello {booking.User.FirstName},</p>" +
                $"<p>Great news! Your extension request for <strong>{booking.ParkingSpace?.Title}</strong> has been approved.</p>" +
                $"<p>Additional charge: <strong>₹{booking.PendingExtensionAmount:F2}</strong>.</p>" +
                "<p>Please log in and complete payment to confirm the extension.</p>");
        }

        await _cache.RemoveAsync($"parking:{booking.ParkingSpaceId}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);

        return new ApiResponse<BookingDto>(true,
            "Extension approved. Awaiting member payment.", booking.ToDto());
    }
}

public class RejectExtensionHandler : ICommandHandler<RejectExtensionCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public RejectExtensionHandler(
        IUnitOfWork unitOfWork,
        INotificationCoordinator notificationCoordinator,
        IEmailService emailService,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _emailService = emailService;
        _cache = cache;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(RejectExtensionCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);

        if (booking.ParkingSpace?.OwnerId != command.VendorId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        try
        {
            booking.RejectExtension(command.Reason ?? "Rejected by parking owner");
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse<BookingDto>(false, ex.Message, null);
        }

        // Notify user of rejection
        await _notificationCoordinator.SendAsync(
            booking.UserId,
            new NotificationRequest(
                NotificationType.BookingRejected.ToString(),
                "Extension Request Rejected",
                $"Your extension request for {booking.ParkingSpace?.Title} was rejected. Reason: {command.Reason ?? "Rejected by parking owner"}",
                NotificationChannels.InApp,
                new Dictionary<string, string>
                {
                    { "BookingId", booking.Id.ToString() },
                    { "BookingReference", booking.BookingReference ?? string.Empty },
                    { "Reason", command.Reason ?? string.Empty },
                    { "Type", "Extension" }
                }),
            cancellationToken);

        // Notify vendor so UI count refreshes
        await _notificationCoordinator.SendAsync(
            booking.ParkingSpace.OwnerId,
            new NotificationRequest(
                NotificationType.BookingRejected.ToString(),
                "Extension Rejected",
                $"You have rejected the extension for {booking.BookingReference}",
                NotificationChannels.InApp,
                new Dictionary<string, string>
                {
                    { "BookingId", booking.Id.ToString() },
                    { "BookingReference", booking.BookingReference ?? string.Empty },
                    { "Type", "Extension" },
                    { "Silent", "true" }
                }),
            cancellationToken);

        if (booking.User?.Email != null)
        {
            await _emailService.SendEmailAsync(
                booking.User.Email,
                $"Extension Rejected: {booking.BookingReference}",
                $"<p>Hello {booking.User.FirstName},</p>" +
                $"<p>We're sorry, but your extension request for <strong>{booking.ParkingSpace?.Title}</strong> was rejected.</p>" +
                $"<p><strong>Reason:</strong> {command.Reason ?? "Rejected by parking owner"}</p>" +
                "<p>Your original booking remains unchanged.</p>");
        }

        await _cache.RemoveAsync($"parking:{booking.ParkingSpaceId}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);

        return new ApiResponse<BookingDto>(true, "Extension request rejected.", booking.ToDto());
    }
}

public class ConfirmExtensionPaymentHandler : ICommandHandler<ConfirmExtensionPaymentCommand, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public ConfirmExtensionPaymentHandler(
        IUnitOfWork unitOfWork,
        INotificationCoordinator notificationCoordinator,
        IEmailService emailService,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _emailService = emailService;
        _cache = cache;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(ConfirmExtensionPaymentCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);

        if (booking.UserId != command.UserId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        var extensionAmount = booking.PendingExtensionAmount ?? 0;
        var newEndDateTime = booking.PendingExtensionEndDateTime;

        try
        {
            booking.ConfirmExtension();
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse<BookingDto>(false, ex.Message, null);
        }

        // Notify vendor of payment
        if (booking.ParkingSpace?.OwnerId != null)
        {
            var memberName = booking.User != null ? $"{booking.User.FirstName} {booking.User.LastName}" : "A member";
            await _notificationCoordinator.SendAsync(
                booking.ParkingSpace.OwnerId,
                new NotificationRequest(
                    NotificationType.PaymentReceived.ToString(),
                    "Extension Payment Received",
                    $"{memberName} has paid ₹{extensionAmount:F2} to extend booking {booking.BookingReference}",
                    NotificationChannels.InApp,
                    new Dictionary<string, string>
                    {
                        { "BookingId", booking.Id.ToString() },
                        { "BookingReference", booking.BookingReference ?? string.Empty },
                        { "Amount", extensionAmount.ToString("F2") },
                        { "Type", "Extension" }
                    }),
                cancellationToken);

            if (booking.ParkingSpace.Owner?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    booking.ParkingSpace.Owner.Email,
                    $"Extension Payment Received: {booking.BookingReference}",
                    $"<p>Hello {booking.ParkingSpace.Owner.FirstName},</p>" +
                    $"<p>Extension payment of <strong>₹{extensionAmount:F2}</strong> received for booking {booking.BookingReference}.</p>" +
                    $"<p>New end time: <strong>{newEndDateTime:f}</strong>.</p>");
            }
        }

        // Notify user of confirmation
        if (booking.User?.Email != null)
        {
            await _emailService.SendEmailAsync(
                booking.User.Email,
                $"Extension Confirmed: {booking.BookingReference}",
                $"<p>Hello {booking.User.FirstName},</p>" +
                $"<p>Your booking extension for <strong>{booking.ParkingSpace?.Title}</strong> has been confirmed.</p>" +
                $"<p>New end time: <strong>{newEndDateTime:f}</strong>. Additional charge: <strong>₹{extensionAmount:F2}</strong>.</p>");
        }

        await _cache.RemoveAsync($"parking:{booking.ParkingSpaceId}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);

        return new ApiResponse<BookingDto>(true,
            $"Extension confirmed. Booking extended to {booking.EndDateTime:f}.", booking.ToDto());
    }
}

