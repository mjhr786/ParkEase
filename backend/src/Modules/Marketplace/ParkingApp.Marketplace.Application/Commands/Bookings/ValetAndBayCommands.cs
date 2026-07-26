using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.Common;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Domain.Interfaces;
using NotificationType = ParkingApp.Messaging.Contracts.Enums.NotificationType;

namespace ParkingApp.Marketplace.Application.Commands.Bookings;

public sealed record RequestValetCommand(
    Guid BookingId,
    Guid UserId,
    string? Notes,
    int? LeadMinutes
) : ICommand<ApiResponse<BookingDto>>;

public sealed record CancelValetCommand(Guid BookingId, Guid UserId) : ICommand<ApiResponse<BookingDto>>;

public sealed record AcknowledgeValetCommand(Guid BookingId, Guid VendorId) : ICommand<ApiResponse<BookingDto>>;

public sealed record MarkValetReadyCommand(Guid BookingId, Guid VendorId) : ICommand<ApiResponse<BookingDto>>;

public sealed record CompleteValetCommand(Guid BookingId, Guid VendorId) : ICommand<ApiResponse<BookingDto>>;

public sealed record AssignBayCommand(
    Guid BookingId,
    Guid VendorId,
    string? FacilityLevel,
    string? FacilityZone,
    string? BayLabel,
    int? SlotNumber
) : ICommand<ApiResponse<BookingDto>>;

internal sealed class RequestValetHandler : ICommandHandler<RequestValetCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationSender _notificationSender;
    private readonly IOptionsMonitor<ValetOptions> _options;
    private readonly ILogger<RequestValetHandler> _logger;

    public RequestValetHandler(
        IMarketplaceUnitOfWork unitOfWork,
        INotificationSender notificationSender,
        IOptionsMonitor<ValetOptions> options,
        ILogger<RequestValetHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationSender = notificationSender;
        _options = options;
        _logger = logger;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(RequestValetCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        if (booking.UserId != command.UserId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        var space = booking.ParkingSpace;
        if (space is null || !space.IsValetEnabled)
            return new ApiResponse<BookingDto>(false, "Valet is not available at this facility", null);

        var opts = _options.CurrentValue;
        var lead = command.LeadMinutes ?? opts.DefaultLeadMinutes;
        lead = Math.Clamp(lead, Math.Max(1, opts.MinLeadMinutes), Math.Max(opts.MinLeadMinutes, opts.MaxLeadMinutes));

        try
        {
            var now = DateTime.UtcNow;
            booking.RequestValet(now, lead, command.Notes);

            await _notificationSender.SendAsync(
                space.OwnerId,
                new NotificationSendRequest(
                    NotificationType.SystemAlert.ToString(),
                    $"Valet request — ready in ~{lead} min",
                    $"Guest requested vehicle for booking {booking.BookingReference} at {space.Title}. Target ready: {booking.ValetTargetReadyAt:u}."
                    + (string.IsNullOrWhiteSpace(booking.ValetNotes) ? "" : $" Note: {booking.ValetNotes}"),
                    Channels: new[] { "InApp" },
                    Data: new Dictionary<string, string>
                    {
                        { "BookingId", booking.Id.ToString() },
                        { "Type", "booking.valet.requested" },
                        { "ValetStatus", booking.ValetStatus.ToString() },
                        { "TargetReadyAt", booking.ValetTargetReadyAt?.ToString("o") ?? "" },
                        { "Path", $"/vendor/bookings?bookingId={booking.Id}" }
                    }),
                cancellationToken);

            booking.MarkValetStaffNotified(now);
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Valet requested for booking {BookingId} (lead {Lead}m)", booking.Id, lead);
            return new ApiResponse<BookingDto>(true, "Valet requested — staff notified", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

internal sealed class CancelValetHandler : ICommandHandler<CancelValetCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public CancelValetHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<BookingDto>> HandleAsync(CancelValetCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        if (booking.UserId != command.UserId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        try
        {
            booking.CancelValet();
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<BookingDto>(true, "Valet request cancelled", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

internal sealed class AcknowledgeValetHandler : ICommandHandler<AcknowledgeValetCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public AcknowledgeValetHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<BookingDto>> HandleAsync(AcknowledgeValetCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        if (booking.ParkingSpace?.OwnerId != command.VendorId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        try
        {
            booking.AcknowledgeValet();
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<BookingDto>(true, "Valet request acknowledged", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

internal sealed class MarkValetReadyHandler : ICommandHandler<MarkValetReadyCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly INotificationSender _notificationSender;

    public MarkValetReadyHandler(IMarketplaceUnitOfWork unitOfWork, INotificationSender notificationSender)
    {
        _unitOfWork = unitOfWork;
        _notificationSender = notificationSender;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(MarkValetReadyCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        if (booking.ParkingSpace?.OwnerId != command.VendorId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        try
        {
            var now = DateTime.UtcNow;
            booking.MarkValetReady(now);

            await _notificationSender.SendAsync(
                booking.UserId,
                new NotificationSendRequest(
                    NotificationType.SystemAlert.ToString(),
                    "Your vehicle is ready",
                    $"Valet at {booking.ParkingSpace?.Title ?? "the facility"} has your vehicle ready (ref {booking.BookingReference}).",
                    Channels: new[] { "InApp" },
                    Data: new Dictionary<string, string>
                    {
                        { "BookingId", booking.Id.ToString() },
                        { "Type", "booking.valet.ready" },
                        { "Path", $"/bookings?bookingId={booking.Id}" }
                    }),
                cancellationToken);

            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<BookingDto>(true, "Vehicle marked ready — guest notified", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

internal sealed class CompleteValetHandler : ICommandHandler<CompleteValetCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public CompleteValetHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<BookingDto>> HandleAsync(CompleteValetCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        if (booking.ParkingSpace?.OwnerId != command.VendorId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        try
        {
            booking.CompleteValet();
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<BookingDto>(true, "Valet completed", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}

internal sealed class AssignBayHandler : ICommandHandler<AssignBayCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public AssignBayHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<BookingDto>> HandleAsync(AssignBayCommand command, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(command.BookingId, cancellationToken);
        if (booking is null)
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        if (booking.ParkingSpace?.OwnerId != command.VendorId)
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);

        try
        {
            booking.AssignBayGuidance(
                command.FacilityLevel,
                command.FacilityZone,
                command.BayLabel,
                command.SlotNumber);
            _unitOfWork.Bookings.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<BookingDto>(true, "Bay assignment updated", booking.ToDto());
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<BookingDto>(ex);
        }
    }
}
