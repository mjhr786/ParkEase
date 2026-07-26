using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingApp.Admin.Contracts;
using ParkingApp.Application.Common;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Application.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.Admin;

public sealed record AdminListBookingsQuery(
    string? Search,
    BookingStatus? Status,
    Guid? UserId,
    Guid? ParkingSpaceId,
    int Page = 1,
    int PageSize = 25) : IQuery<ApiResponse<AdminBookingPageDto>>;

public sealed record AdminGetBookingQuery(Guid BookingId) : IQuery<ApiResponse<AdminBookingDetailDto>>;

public sealed record AdminCancelBookingCommand(
    Guid ActorAdminUserId,
    string ActorEmail,
    Guid BookingId,
    string Reason,
    string? IpAddress,
    string? UserAgent) : ICommand<ApiResponse<AdminBookingDetailDto>>;

internal sealed class AdminListBookingsHandler : IQueryHandler<AdminListBookingsQuery, ApiResponse<AdminBookingPageDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public AdminListBookingsHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<AdminBookingPageDto>> HandleAsync(
        AdminListBookingsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 25 : Math.Min(query.PageSize, 100);

        var (items, total) = await _unitOfWork.Bookings.SearchForAdminAsync(
            query.Search,
            query.Status,
            query.UserId,
            query.ParkingSpaceId,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(b => new AdminBookingListItemDto(
            b.Id,
            b.BookingReference,
            b.UserId,
            b.ParkingSpaceId,
            b.ParkingSpace?.Title,
            b.Status,
            b.StartDateTime,
            b.EndDateTime,
            b.TotalAmount,
            b.VehicleNumber,
            b.CreatedAt)).ToList();

        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new ApiResponse<AdminBookingPageDto>(
            true,
            null,
            new AdminBookingPageDto(dtos, total, page, pageSize, totalPages));
    }
}

internal sealed class AdminGetBookingHandler : IQueryHandler<AdminGetBookingQuery, ApiResponse<AdminBookingDetailDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public AdminGetBookingHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<AdminBookingDetailDto>> HandleAsync(
        AdminGetBookingQuery query,
        CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(query.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<AdminBookingDetailDto>(false, "Booking not found", null);

        return new ApiResponse<AdminBookingDetailDto>(true, null, ToDetail(booking));
    }

    internal static AdminBookingDetailDto ToDetail(Domain.Entities.Booking b) =>
        new(
            b.Id,
            b.BookingReference,
            b.UserId,
            b.ParkingSpaceId,
            b.ParkingSpace?.Title,
            b.ParkingSpace?.OwnerId,
            b.Status,
            b.StartDateTime,
            b.EndDateTime,
            b.BaseAmount,
            b.TaxAmount,
            b.ServiceFee,
            b.TotalAmount,
            b.VehicleNumber,
            b.CancellationReason,
            b.CancelledAt,
            b.Payment?.Id,
            b.Payment?.Status,
            b.Payment?.Amount,
            b.Payment?.RefundAmount ?? b.RefundAmount,
            b.HasPendingExtension,
            b.CreatedAt);
}

internal sealed class AdminCancelBookingHandler
    : ICommandHandler<AdminCancelBookingCommand, ApiResponse<AdminBookingDetailDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IAdminAudit _audit;
    private readonly IEmailService _emailService;
    private readonly IUserLookup _userLookup;
    private readonly ILogger<AdminCancelBookingHandler> _logger;

    public AdminCancelBookingHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IAdminAudit audit,
        IEmailService emailService,
        IUserLookup userLookup,
        ILogger<AdminCancelBookingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
        _emailService = emailService;
        _userLookup = userLookup;
        _logger = logger;
    }

    public async Task<ApiResponse<AdminBookingDetailDto>> HandleAsync(
        AdminCancelBookingCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            return new ApiResponse<AdminBookingDetailDto>(false, "Reason is required", null);

        var reason = command.Reason.Trim();
        if (reason.Length > 500)
            return new ApiResponse<AdminBookingDetailDto>(false, "Reason must be at most 500 characters", null);

        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<AdminBookingDetailDto>(false, "Booking not found", null);

        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled)
            return new ApiResponse<AdminBookingDetailDto>(
                false,
                $"Cannot cancel booking in {booking.Status} status",
                null);

        var previousStatus = booking.Status;

        try
        {
            // Raises BookingCancelledEvent → cache + notifications after SaveChanges
            booking.Cancel($"[Admin] {reason}");

            if (booking.EventParkingPackageId is Guid packageId)
            {
                var package = await _unitOfWork.EventParkingPackages.GetByIdAsync(packageId, cancellationToken);
                if (package is not null)
                {
                    package.ReleaseSale();
                    _unitOfWork.EventParkingPackages.Update(package);
                }
            }

            _unitOfWork.Bookings.Update(booking);

            _audit.Stage(new AdminAuditEntry(
                command.ActorAdminUserId,
                command.ActorEmail,
                "Booking.ForceCancel",
                "Booking",
                booking.Id,
                JsonSerializer.Serialize(new
                {
                    reason,
                    previousStatus = previousStatus.ToString(),
                    bookingReference = booking.BookingReference
                }),
                command.IpAddress,
                command.UserAgent));

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var ownerId = booking.ParkingSpace?.OwnerId;
            var member = await _userLookup.GetByIdAsync(booking.UserId, cancellationToken);
            var owner = ownerId is Guid oid ? await _userLookup.GetByIdAsync(oid, cancellationToken) : null;
            var spaceTitle = booking.ParkingSpace?.Title ?? "parking space";

            if (owner is not null && !string.IsNullOrWhiteSpace(owner.Email))
            {
                await _emailService.SendEmailAsync(
                    owner.Email,
                    $"Booking Cancelled by Admin: {booking.BookingReference}",
                    $"<p>Hello {owner.FirstName},</p><p>The booking {booking.BookingReference} for <strong>{spaceTitle}</strong> was cancelled by a platform administrator.</p><p>Reason: {System.Net.WebUtility.HtmlEncode(reason)}</p>");
            }

            if (member is not null && !string.IsNullOrWhiteSpace(member.Email))
            {
                await _emailService.SendEmailAsync(
                    member.Email,
                    $"Booking Cancelled: {booking.BookingReference}",
                    $"<p>Hello {member.FirstName},</p><p>Your booking {booking.BookingReference} for <strong>{spaceTitle}</strong> was cancelled by platform support.</p><p>Reason: {System.Net.WebUtility.HtmlEncode(reason)}</p>");
            }

            _logger.LogInformation(
                "Admin {ActorId} force-cancelled booking {BookingId}. Reason: {Reason}",
                command.ActorAdminUserId,
                booking.Id,
                reason);

            // Reload payment nav if needed
            booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(booking.Id, cancellationToken)
                ?? booking;

            return new ApiResponse<AdminBookingDetailDto>(
                true,
                "Booking cancelled",
                AdminGetBookingHandler.ToDetail(booking));
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<AdminBookingDetailDto>(ex);
        }
    }
}
