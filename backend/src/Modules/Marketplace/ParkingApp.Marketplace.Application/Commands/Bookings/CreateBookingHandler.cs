using ParkingApp.Application.CQRS;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Application.Caching;
using ParkingApp.Application.Common;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.Application.Interfaces;

using ParkingApp.Marketplace.Application.Mappings;


using ParkingApp.Marketplace.Application.Services;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.BuildingBlocks.Extensions;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Identity.Contracts;

namespace ParkingApp.Marketplace.Application.Commands.Bookings;

internal sealed class CreateBookingHandler : ICommandHandler<CreateBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IParkingPassPricingService _pricingService;
    private readonly IBookingAvailabilityService _availability;

    public CreateBookingHandler(
        IMarketplaceUnitOfWork unitOfWork,
        ICacheService cache,
        IParkingPassPricingService pricingService,
        IBookingAvailabilityService availability)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _pricingService = pricingService;
        _availability = availability;
    }

    public CreateBookingHandler(
        IMarketplaceUnitOfWork unitOfWork,
        ICacheService cache)
        : this(
            unitOfWork,
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
        // BookingRequestedEvent ΓåÆ outbox ΓåÆ email + in-app notification handlers
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await CacheInvalidation.ForBookingChangeAsync(
            _cache,
            command.ParkingSpaceId,
            memberId: booking.UserId,
            vendorId: parking.OwnerId,
            cancellationToken);

        // DTO without reload: navigations may be null ΓÇö ToDto tolerates Unknown for names/address.
        return new ApiResponse<BookingDto>(true, "Booking created successfully", booking.ToDto());
    }
}

internal sealed class CancelBookingHandler : ICommandHandler<CancelBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IUserLookup _userLookup;

    public CancelBookingHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IEmailService emailService,
        IUserLookup userLookup)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _userLookup = userLookup;
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
            // Raises BookingCancelledEvent ΓåÆ cache + owner push via domain event handlers after SaveChanges
            booking.Cancel(command.Reason);
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Emails via Identity contract (no User navigations on Marketplace entities)
            var ownerId = booking.ParkingSpace?.OwnerId;
            var member = await _userLookup.GetByIdAsync(booking.UserId, cancellationToken);
            var owner = ownerId is Guid oid ? await _userLookup.GetByIdAsync(oid, cancellationToken) : null;
            var spaceTitle = booking.ParkingSpace?.Title ?? "parking space";

            if (owner is not null && !string.IsNullOrWhiteSpace(owner.Email))
            {
                await _emailService.SendEmailAsync(
                    owner.Email,
                    $"Booking Cancelled: {booking.BookingReference}",
                    $"<p>Hello {owner.FirstName},</p><p>The booking {booking.BookingReference} for <strong>{spaceTitle}</strong> has been cancelled.</p>");
            }

            if (member is not null && !string.IsNullOrWhiteSpace(member.Email))
            {
                await _emailService.SendEmailAsync(
                    member.Email,
                    $"Booking Cancelled: {booking.BookingReference}",
                    $"<p>Hello {member.FirstName},</p><p>The booking {booking.BookingReference} for <strong>{spaceTitle}</strong> has been cancelled.</p>");
            }

            return new ApiResponse<BookingDto>(true, "Booking cancelled", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

internal sealed class ApproveBookingHandler : ICommandHandler<ApproveBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public ApproveBookingHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
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
                // BookingConfirmedEvent ΓåÆ outbox notification/email handlers
                booking.Confirm();
            }
            else
            {
                // BookingApprovedEvent ΓåÆ outbox notification/email handlers
                booking.AwaitPayment();
            }

            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await CacheInvalidation.ForBookingChangeAsync(
                _cache,
                booking.ParkingSpaceId,
                memberId: booking.UserId,
                vendorId: booking.ParkingSpace?.OwnerId,
                cancellationToken);

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

internal sealed class RejectBookingHandler : ICommandHandler<RejectBookingCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public RejectBookingHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
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
            // BookingRejectedEvent ΓåÆ outbox notification + email handlers
            booking.Reject(command.Reason ?? "Rejected by vendor", vendorUserId: command.VendorId);
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await CacheInvalidation.ForBookingChangeAsync(
                _cache,
                booking.ParkingSpaceId,
                memberId: booking.UserId,
                vendorId: booking.ParkingSpace?.OwnerId,
                cancellationToken);

            return new ApiResponse<BookingDto>(true, "Booking rejected", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

internal sealed class CheckInHandler : ICommandHandler<CheckInCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public CheckInHandler(IMarketplaceUnitOfWork unitOfWork)
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
            // Raises BookingCheckedInEvent ΓåÆ outbox ΓåÆ BookingCheckedInNotificationHandler
            booking.CheckIn();
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ApiResponse<BookingDto>(true, "Checked in successfully", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

internal sealed class CheckOutHandler : ICommandHandler<CheckOutCommand, ApiResponse<BookingDto>>
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

internal sealed class RequestExtensionHandler : ICommandHandler<RequestExtensionCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IParkingPassPricingService _pricingService;
    private readonly IBookingAvailabilityService _availability;

    public RequestExtensionHandler(
        IMarketplaceUnitOfWork unitOfWork,
        ICacheService cache,
        IParkingPassPricingService pricingService,
        IBookingAvailabilityService availability)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _pricingService = pricingService;
        _availability = availability;
    }

    public RequestExtensionHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache)
        : this(unitOfWork, cache, new ParkingPassPricingService(unitOfWork), new BookingAvailabilityService(unitOfWork))
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

        // Price only the extended window (current end → new end), matching the member UI quote.
        // Full-booking reprice often yields 0 extra (daily/weekly unit ceilings or pass covering
        // the original period), which skipped AwaitingExtensionPayment and hid the pay button.
        var extensionPricing = await _pricingService.CalculateAsync(
            command.UserId,
            parking,
            booking.EndDateTime,
            newEndDateTime,
            booking.PricingType,
            booking.DiscountCode,
            booking.Id,
            cancellationToken);

        var totalExtra = Math.Max(0, extensionPricing.TotalAmount);

        try
        {
            // BookingExtensionRequestedEvent → outbox notification/email handlers
            booking.RequestExtension(newEndDateTime, totalExtra);
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }

        await CacheInvalidation.ForBookingChangeAsync(
            _cache,
            booking.ParkingSpaceId,
            memberId: booking.UserId,
            vendorId: parking.OwnerId,
            cancellationToken);

        return new ApiResponse<BookingDto>(true,
            "Extension request submitted. Awaiting owner approval.", booking.ToDto());
    }
}

internal sealed class ApproveExtensionHandler : ICommandHandler<ApproveExtensionCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public ApproveExtensionHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
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
                // BookingExtensionApprovedEvent ΓåÆ outbox handlers
                booking.ApproveExtension(vendorUserId: command.VendorId);
            }
            else
            {
                // BookingExtensionConfirmedEvent ΓåÆ outbox handlers
                booking.ConfirmExtension();
            }

            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }

        await CacheInvalidation.ForBookingChangeAsync(
            _cache,
            booking.ParkingSpaceId,
            memberId: booking.UserId,
            vendorId: booking.ParkingSpace?.OwnerId,
            cancellationToken);

        return new ApiResponse<BookingDto>(
            true,
            requiresExtensionPayment ? "Extension approved. Awaiting member payment." : "Extension approved and confirmed with parking pass pricing.",
            booking.ToDto());
    }
}

internal sealed class RejectExtensionHandler : ICommandHandler<RejectExtensionCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public RejectExtensionHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
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
            // BookingExtensionRejectedEvent ΓåÆ outbox handlers
            booking.RejectExtension(command.Reason ?? "Rejected by parking owner", vendorUserId: command.VendorId);
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }

        await CacheInvalidation.ForBookingChangeAsync(
            _cache,
            booking.ParkingSpaceId,
            memberId: booking.UserId,
            vendorId: booking.ParkingSpace?.OwnerId,
            cancellationToken);

        return new ApiResponse<BookingDto>(true, "Extension request rejected.", booking.ToDto());
    }
}

internal sealed class ConfirmExtensionPaymentHandler : ICommandHandler<ConfirmExtensionPaymentCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public ConfirmExtensionPaymentHandler(IMarketplaceUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(ConfirmExtensionPaymentCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking == null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);

        if (booking.UserId != command.UserId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        try
        {
            // BookingExtensionConfirmedEvent ΓåÆ outbox handlers
            booking.ConfirmExtension();
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }

        await CacheInvalidation.ForBookingChangeAsync(
            _cache,
            booking.ParkingSpaceId,
            memberId: booking.UserId,
            vendorId: booking.ParkingSpace?.OwnerId,
            cancellationToken);

        return new ApiResponse<BookingDto>(true,
            $"Extension confirmed. Booking extended to {booking.EndDateTime:f}.", booking.ToDto());
    }
}


