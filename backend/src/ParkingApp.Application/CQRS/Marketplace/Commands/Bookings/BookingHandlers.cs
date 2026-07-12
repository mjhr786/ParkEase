using ParkingApp.Application.Common;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Application.Services;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.BuildingBlocks.Extensions;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Bookings;

public class CreateBookingHandler : ICommandHandler<CreateBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;
    private readonly IParkingPassPricingService _pricingService;
    private readonly IBookingAvailabilityService _availability;

    public CreateBookingHandler(
        IMarketplaceUnitOfWork unitOfWork,
        INotificationCoordinator notificationCoordinator,
        IEmailService emailService,
        ICacheService cache,
        IParkingPassPricingService pricingService,
        IBookingAvailabilityService availability)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _emailService = emailService;
        _cache = cache;
        _pricingService = pricingService;
        _availability = availability;
    }

    public CreateBookingHandler(
        IMarketplaceUnitOfWork unitOfWork,
        INotificationCoordinator notificationCoordinator,
        IEmailService emailService,
        ICacheService cache)
        : this(
            unitOfWork,
            notificationCoordinator,
            emailService,
            cache,
            new ParkingPassPricingService(unitOfWork),
            new BookingAvailabilityService(unitOfWork))
    {
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(CreateBookingCommand command, CancellationToken cancellationToken = default)
    {
        var startDateTimeUtc = command.StartDateTime.ToUtc();
        var endDateTimeUtc = command.EndDateTime.ToUtc();

        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<BookingDto>(false, "Parking space is not available", null);
        }

        var availability = await _availability.CanCreateAsync(
            command.UserId,
            parking,
            startDateTimeUtc,
            endDateTimeUtc,
            command.SlotNumber,
            command.VehicleNumber,
            cancellationToken);

        if (!availability.IsAllowed)
        {
            return new ApiResponse<BookingDto>(false, availability.ErrorMessage ?? "Booking not available", null);
        }

        var pricing = await _pricingService.CalculateAsync(
            command.UserId,
            parking,
            startDateTimeUtc,
            endDateTimeUtc,
            command.PricingType,
            command.DiscountCode,
            null,
            cancellationToken);

        var booking = Booking.CreateMarketplace(
            command.UserId,
            command.ParkingSpaceId,
            startDateTimeUtc,
            endDateTimeUtc,
            command.PricingType,
            command.VehicleType,
            pricing.BaseAmount,
            pricing.TaxAmount,
            pricing.ServiceFee,
            pricing.DiscountAmount,
            pricing.TotalAmount,
            pricing.IsPassApplied ? null : command.DiscountCode,
            pricing.ParkingPassId,
            command.SlotNumber,
            command.VehicleNumber,
            command.VehicleModel,
            command.VehicleColor);

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
                NotificationChannels.All,
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
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;

    public CancelBookingHandler(IMarketplaceUnitOfWork unitOfWork, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
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
            // Raises BookingCancelledEvent → cache + owner push via domain event handlers after SaveChanges
            booking.Cancel(command.Reason);
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Emails stay in the command handler (template-heavy); push/cache are event-driven
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

            return new ApiResponse<BookingDto>(true, "Booking cancelled", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

public class ApproveBookingHandler : ICommandHandler<ApproveBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public ApproveBookingHandler(IMarketplaceUnitOfWork unitOfWork, INotificationCoordinator notificationCoordinator, IEmailService emailService, ICacheService cache)
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

        var ownerId = booking.ParkingSpace?.OwnerId;
        if (ownerId != command.VendorId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        try
        {
            var isPassCoveredBooking = booking.ParkingPassId.HasValue && booking.TotalAmount <= 0;
            if (isPassCoveredBooking)
            {
                booking.Confirm();
            }
            else
            {
                booking.AwaitPayment();
            }

            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Notify member that their booking was approved
            await _notificationCoordinator.SendAsync(
                booking.UserId,
                new NotificationRequest(
                    NotificationType.BookingConfirmed.ToString(),
                    isPassCoveredBooking ? "Booking Confirmed!" : "Booking Approved!",
                    isPassCoveredBooking
                        ? $"Your booking for {booking.ParkingSpace?.Title} has been approved and confirmed with your parking pass pricing."
                        : booking.TotalAmount > 0
                            ? $"Your booking for {booking.ParkingSpace?.Title} has been approved. Please complete payment."
                            : $"Your booking for {booking.ParkingSpace?.Title} has been approved and is awaiting final settlement.",
                    NotificationChannels.All,
                    new Dictionary<string, string> { { "BookingId", command.BookingId.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty } }
                ),
                cancellationToken);

            // Notify vendor (owner) so UI count refreshes
            await _notificationCoordinator.SendAsync(
                ownerId.Value,
                new NotificationRequest(
                    NotificationType.BookingConfirmed.ToString(),
                    "Booking Approved",
                    $"You have approved booking {booking.BookingReference}",
                    NotificationChannels.All,
                    new Dictionary<string, string> { { "BookingId", command.BookingId.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty }, { "Silent", "true" } }
                ),
                cancellationToken);

            // Send Email to Member
            if (booking.User?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    booking.User.Email,
                    isPassCoveredBooking ? $"Booking Confirmed: {booking.BookingReference}" : $"Booking Approved: {booking.BookingReference}",
                    isPassCoveredBooking
                        ? $"<p>Hello {booking.User.FirstName},</p><p>Great news! Your booking for <strong>{booking.ParkingSpace?.Title}</strong> has been approved and confirmed.</p><p>Your active parking pass covered this booking, so no payment is required.</p>"
                        : booking.TotalAmount > 0
                            ? $"<p>Hello {booking.User.FirstName},</p><p>Great news! Your booking for <strong>{booking.ParkingSpace?.Title}</strong> has been approved.</p><p>Please log in and complete your payment to confirm the reservation.</p>"
                            : $"<p>Hello {booking.User.FirstName},</p><p>Great news! Your booking for <strong>{booking.ParkingSpace?.Title}</strong> has been approved.</p><p>No payment is due, but the booking will remain pending final settlement until it is fully confirmed.</p>"
                );
            }

            // Invalidate caches
            await _cache.RemoveAsync($"parking:{booking.ParkingSpaceId}", cancellationToken);
            await _cache.RemoveByPatternAsync("search:*", cancellationToken);

            return new ApiResponse<BookingDto>(
                true,
                isPassCoveredBooking ? "Booking approved and confirmed with parking pass pricing" : "Booking approved, awaiting final settlement",
                booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

public class RejectBookingHandler : ICommandHandler<RejectBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public RejectBookingHandler(IMarketplaceUnitOfWork unitOfWork, INotificationCoordinator notificationCoordinator, IEmailService emailService, ICacheService cache)
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

        var ownerId = booking.ParkingSpace?.OwnerId;
        if (ownerId != command.VendorId)
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
                    NotificationChannels.All,
                    new Dictionary<string, string> { { "BookingId", command.BookingId.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty }, { "Reason", booking.CancellationReason ?? string.Empty } }
                ),
                cancellationToken);

            // Notify vendor (owner) so UI count refreshes
            await _notificationCoordinator.SendAsync(
                ownerId.Value,
                new NotificationRequest(
                    NotificationType.BookingRejected.ToString(),
                    "Booking Rejected",
                    $"You have rejected booking {booking.BookingReference}",
                    NotificationChannels.All,
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
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

public class CheckInHandler : ICommandHandler<CheckInCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;

    public CheckInHandler(IMarketplaceUnitOfWork unitOfWork, INotificationCoordinator notificationCoordinator)
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
                        NotificationChannels.All,
                        new Dictionary<string, string> { { "BookingId", command.BookingId.ToString() }, { "BookingReference", booking.BookingReference ?? string.Empty } }
                    ),
                    cancellationToken);
            }

            return new ApiResponse<BookingDto>(true, "Checked in successfully", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

public class CheckOutHandler : ICommandHandler<CheckOutCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public CheckOutHandler(IMarketplaceUnitOfWork unitOfWork)
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
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

public class RequestExtensionHandler : ICommandHandler<RequestExtensionCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;
    private readonly IParkingPassPricingService _pricingService;
    private readonly IBookingAvailabilityService _availability;

    public RequestExtensionHandler(
        IMarketplaceUnitOfWork unitOfWork,
        INotificationCoordinator notificationCoordinator,
        IEmailService emailService,
        ICacheService cache,
        IParkingPassPricingService pricingService,
        IBookingAvailabilityService availability)
    {
        _unitOfWork = unitOfWork;
        _notificationCoordinator = notificationCoordinator;
        _emailService = emailService;
        _cache = cache;
        _pricingService = pricingService;
        _availability = availability;
    }

    public RequestExtensionHandler(
        IMarketplaceUnitOfWork unitOfWork,
        INotificationCoordinator notificationCoordinator,
        IEmailService emailService,
        ICacheService cache)
        : this(
            unitOfWork,
            notificationCoordinator,
            emailService,
            cache,
            new ParkingPassPricingService(unitOfWork),
            new BookingAvailabilityService(unitOfWork))
    {
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(RequestExtensionCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);

        if (booking.UserId != command.UserId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        var newEndDateTime = command.NewEndDateTime.ToUtc();
        var parking = booking.ParkingSpace
            ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(booking.ParkingSpaceId, cancellationToken);

        if (parking == null)
            return new ApiResponse<BookingDto>(false, "Parking space not found", null);

        var availability = await _availability.CanExtendAsync(
            booking, parking, booking.EndDateTime, newEndDateTime, cancellationToken);
        if (!availability.IsAllowed)
            return new ApiResponse<BookingDto>(false, availability.ErrorMessage ?? "Extension not available", null);

        var repricedBooking = await _pricingService.CalculateAsync(
            command.UserId,
            parking,
            booking.StartDateTime,
            newEndDateTime,
            booking.PricingType,
            booking.DiscountCode,
            booking.Id,
            cancellationToken);

        var totalExtra = Math.Max(0, repricedBooking.TotalAmount - booking.TotalAmount);
        var requiresExtensionPayment = totalExtra > 0;

        try
        {
            booking.RequestExtension(newEndDateTime, totalExtra);
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }

        // Notify vendor
        var memberName = booking.User != null ? $"{booking.User.FirstName} {booking.User.LastName}" : "A member";
        await _notificationCoordinator.SendAsync(
            parking.OwnerId,
            new NotificationRequest(
                NotificationType.BookingRequest.ToString(),
                "Extension Request",
                $"{memberName} has requested an extension for booking {booking.BookingReference} at {parking.Title}",
                NotificationChannels.All,
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
                (requiresExtensionPayment
                    ? $"<p>If approved, an additional charge of <strong>INR {totalExtra:F2}</strong> will be due.</p>"
                    : "<p>If approved, your active parking pass pricing means no additional payment will be required.</p>") +
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
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public ApproveExtensionHandler(
        IMarketplaceUnitOfWork unitOfWork,
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

        var ownerId = booking.ParkingSpace?.OwnerId;
        if (ownerId != command.VendorId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        var pendingExtensionAmount = booking.PendingExtensionAmount ?? 0m;
        var requiresExtensionPayment = pendingExtensionAmount > 0;

        try
        {
            if (requiresExtensionPayment)
            {
                booking.ApproveExtension();
            }
            else
            {
                booking.ConfirmExtension();
            }

            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }

        // Notify user
        await _notificationCoordinator.SendAsync(
            booking.UserId,
            new NotificationRequest(
                NotificationType.BookingConfirmed.ToString(),
                requiresExtensionPayment ? "Extension Approved!" : "Extension Confirmed!",
                requiresExtensionPayment
                    ? $"Your extension request for {booking.ParkingSpace?.Title} was approved. Please complete the payment."
                    : $"Your extension request for {booking.ParkingSpace?.Title} was approved and confirmed with your parking pass pricing.",
                NotificationChannels.All,
                new Dictionary<string, string>
                {
                    { "BookingId", booking.Id.ToString() },
                    { "BookingReference", booking.BookingReference ?? string.Empty },
                    { "Type", "Extension" }
                }),
            cancellationToken);

        // Notify vendor so UI count refreshes
        await _notificationCoordinator.SendAsync(
            ownerId.Value,
            new NotificationRequest(
                NotificationType.BookingConfirmed.ToString(),
                "Extension Approved",
                $"You have approved the extension for {booking.BookingReference}",
                NotificationChannels.All,
                new Dictionary<string, string>
                {
                    { "BookingId", booking.Id.ToString() },
                    { "BookingReference", booking.BookingReference ?? string.Empty },
                    { "Type", "Extension" },
                    { "Silent", "true" }
                }),
            cancellationToken);

        if (booking.User?.Email != null && !requiresExtensionPayment)
        {
            await _emailService.SendEmailAsync(
                booking.User.Email,
                $"Extension Confirmed: {booking.BookingReference}",
                $"<p>Hello {booking.User.FirstName},</p>" +
                $"<p>Great news! Your extension request for <strong>{booking.ParkingSpace?.Title}</strong> has been approved and confirmed.</p>" +
                "<p>Your active parking pass covered the extension, so no additional payment is required.</p>");
        }

        if (booking.User?.Email != null && requiresExtensionPayment)
        {
            await _emailService.SendEmailAsync(
                booking.User.Email,
                $"Extension Approved: {booking.BookingReference}",
                $"<p>Hello {booking.User.FirstName},</p>" +
                $"<p>Great news! Your extension request for <strong>{booking.ParkingSpace?.Title}</strong> has been approved.</p>" +
                $"<p>Additional charge: <strong>INR {pendingExtensionAmount:F2}</strong>.</p>" +
                "<p>Please log in and complete payment to confirm the extension.</p>");
        }

        await _cache.RemoveAsync($"parking:{booking.ParkingSpaceId}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);

        return new ApiResponse<BookingDto>(
            true,
            requiresExtensionPayment ? "Extension approved. Awaiting member payment." : "Extension approved and confirmed with parking pass pricing.",
            booking.ToDto());
    }
}

public class RejectExtensionHandler : ICommandHandler<RejectExtensionCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public RejectExtensionHandler(
        IMarketplaceUnitOfWork unitOfWork,
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

        var ownerId = booking.ParkingSpace?.OwnerId;
        if (ownerId != command.VendorId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        try
        {
            booking.RejectExtension(command.Reason ?? "Rejected by parking owner");
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }

        // Notify user of rejection
        await _notificationCoordinator.SendAsync(
            booking.UserId,
            new NotificationRequest(
                NotificationType.BookingRejected.ToString(),
                "Extension Request Rejected",
                $"Your extension request for {booking.ParkingSpace?.Title} was rejected. Reason: {command.Reason ?? "Rejected by parking owner"}",
                NotificationChannels.All,
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
            ownerId.Value,
            new NotificationRequest(
                NotificationType.BookingRejected.ToString(),
                "Extension Rejected",
                $"You have rejected the extension for {booking.BookingReference}",
                NotificationChannels.All,
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
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationCoordinator _notificationCoordinator;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cache;

    public ConfirmExtensionPaymentHandler(
        IMarketplaceUnitOfWork unitOfWork,
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
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
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
                    NotificationChannels.All,
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

