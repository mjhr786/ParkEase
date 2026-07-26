using Microsoft.Extensions.Logging;
using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.EventPackages;

internal static class EventPackageMapper
{
    private const decimal TaxRate = 0.18m;
    private const decimal ServiceFeeRate = 0.05m;

    public static EventParkingPackageDto ToDto(EventParkingPackage package, DateTime? asOfUtc = null)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        return new EventParkingPackageDto(
            package.Id,
            package.ParkingSpaceId,
            package.ParkingSpace?.Title ?? "Parking",
            package.ParkingSpace?.Address ?? string.Empty,
            package.ParkingSpace?.City,
            package.Title,
            package.Description,
            package.EventName,
            package.VenueName,
            package.EventStartUtc,
            package.EventEndUtc,
            package.SalesStartUtc,
            package.SalesEndUtc,
            package.PackagePrice,
            package.TotalSpots,
            package.SoldCount,
            package.AvailableSpots,
            package.IsActive,
            package.IsOnSale(now),
            package.CreatedAt,
            package.VenueEventId,
            package.ZoneName,
            package.EarlyEntryMinutes,
            package.LateExitMinutes,
            package.AccessStartUtc,
            package.AccessEndUtc);
    }

    public static (decimal Tax, decimal Service, decimal Total) PricePackage(decimal packagePrice)
    {
        var tax = Math.Round(packagePrice * TaxRate, 2, MidpointRounding.AwayFromZero);
        var service = Math.Round(packagePrice * ServiceFeeRate, 2, MidpointRounding.AwayFromZero);
        var total = Math.Max(0, packagePrice + tax + service);
        return (tax, service, total);
    }

    /// <summary>
    /// Revenue counts Confirmed / InProgress / Completed / PendingExtension / AwaitingExtensionPayment.
    /// Excludes Cancelled, Rejected, Pending, AwaitingPayment (not paid yet).
    /// </summary>
    public static bool CountsTowardRevenue(BookingStatus status) =>
        status is BookingStatus.Confirmed
            or BookingStatus.InProgress
            or BookingStatus.Completed
            or BookingStatus.PendingExtension
            or BookingStatus.AwaitingExtensionPayment;

    public static EventPackageAnalyticsDto ToAnalytics(
        EventParkingPackage package,
        IReadOnlyList<Booking> packageBookings,
        DateTime? asOfUtc = null)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        var revenueBookings = packageBookings
            .Where(b => CountsTowardRevenue(b.Status))
            .ToList();
        var gross = revenueBookings.Sum(b => b.TotalAmount);
        var sellThrough = package.TotalSpots <= 0
            ? 0m
            : Math.Round(100m * package.SoldCount / package.TotalSpots, 1, MidpointRounding.AwayFromZero);

        return new EventPackageAnalyticsDto(
            package.Id,
            package.VenueEventId,
            package.ParkingSpaceId,
            package.ParkingSpace?.Title ?? "Parking",
            package.Title,
            package.EventName,
            package.VenueName,
            package.ZoneName,
            package.EventStartUtc,
            package.EventEndUtc,
            package.AccessStartUtc,
            package.AccessEndUtc,
            package.PackagePrice,
            package.TotalSpots,
            package.SoldCount,
            package.AvailableSpots,
            sellThrough,
            revenueBookings.Count,
            gross,
            package.IsActive,
            package.IsOnSale(now));
    }
}

internal sealed class CreateEventParkingPackageHandler
    : ICommandHandler<CreateEventParkingPackageCommand, ApiResponse<EventParkingPackageDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ILogger<CreateEventParkingPackageHandler> _logger;

    public CreateEventParkingPackageHandler(
        IMarketplaceUnitOfWork unitOfWork,
        ILogger<CreateEventParkingPackageHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<EventParkingPackageDto>> HandleAsync(
        CreateEventParkingPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.Dto.ParkingSpaceId, cancellationToken);
        if (parking is null)
            return new ApiResponse<EventParkingPackageDto>(false, "Parking space not found", null);
        if (!command.IsAdmin && parking.OwnerId != command.ActorUserId)
            return new ApiResponse<EventParkingPackageDto>(false, "Unauthorized", null);

        try
        {
            var package = EventParkingPackage.Create(
                command.Dto.ParkingSpaceId,
                command.ActorUserId,
                command.Dto.Title,
                command.Dto.EventStartUtc.ToUniversalTime(),
                command.Dto.EventEndUtc.ToUniversalTime(),
                command.Dto.PackagePrice,
                command.Dto.TotalSpots,
                command.Dto.Description,
                command.Dto.EventName,
                command.Dto.VenueName,
                command.Dto.SalesStartUtc?.ToUniversalTime(),
                command.Dto.SalesEndUtc?.ToUniversalTime(),
                command.Dto.VenueEventId,
                command.Dto.ZoneName,
                command.Dto.EarlyEntryMinutes,
                command.Dto.LateExitMinutes);

            await _unitOfWork.EventParkingPackages.AddAsync(package, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            package = await _unitOfWork.EventParkingPackages.GetByIdWithSpaceAsync(package.Id, cancellationToken)
                ?? package;

            _logger.LogInformation(
                "Event package {PackageId} created for space {SpaceId} venueEvent {VenueEventId}",
                package.Id,
                package.ParkingSpaceId,
                package.VenueEventId);

            return new ApiResponse<EventParkingPackageDto>(
                true,
                "Event package created",
                EventPackageMapper.ToDto(package));
        }
        catch (DomainException ex)
        {
            return new ApiResponse<EventParkingPackageDto>(false, ex.Message, null);
        }
    }
}

internal sealed class UpdateEventParkingPackageHandler
    : ICommandHandler<UpdateEventParkingPackageCommand, ApiResponse<EventParkingPackageDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public UpdateEventParkingPackageHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<EventParkingPackageDto>> HandleAsync(
        UpdateEventParkingPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        var package = await _unitOfWork.EventParkingPackages.GetByIdWithSpaceAsync(command.PackageId, cancellationToken);
        if (package is null)
            return new ApiResponse<EventParkingPackageDto>(false, "Event package not found", null);

        var parking = package.ParkingSpace
            ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(package.ParkingSpaceId, cancellationToken);
        if (parking is null)
            return new ApiResponse<EventParkingPackageDto>(false, "Parking space not found", null);
        if (!command.IsAdmin && parking.OwnerId != command.ActorUserId)
            return new ApiResponse<EventParkingPackageDto>(false, "Unauthorized", null);

        try
        {
            var dto = command.Dto;
            package.UpdateDetails(
                title: dto.Title,
                description: dto.Description,
                eventName: dto.EventName,
                venueName: dto.VenueName,
                eventStartUtc: dto.EventStartUtc?.ToUniversalTime(),
                eventEndUtc: dto.EventEndUtc?.ToUniversalTime(),
                packagePrice: dto.PackagePrice,
                totalSpots: dto.TotalSpots,
                salesStartUtc: dto.SalesStartUtc?.ToUniversalTime(),
                salesEndUtc: dto.SalesEndUtc?.ToUniversalTime(),
                isActive: dto.IsActive,
                venueEventId: dto.VenueEventId,
                zoneName: dto.ZoneName,
                earlyEntryMinutes: dto.EarlyEntryMinutes,
                lateExitMinutes: dto.LateExitMinutes);

            _unitOfWork.EventParkingPackages.Update(package);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ApiResponse<EventParkingPackageDto>(
                true,
                "Event package updated",
                EventPackageMapper.ToDto(package));
        }
        catch (DomainException ex)
        {
            return new ApiResponse<EventParkingPackageDto>(false, ex.Message, null);
        }
    }
}

internal sealed class DeactivateEventParkingPackageHandler
    : ICommandHandler<DeactivateEventParkingPackageCommand, ApiResponse<bool>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public DeactivateEventParkingPackageHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<bool>> HandleAsync(
        DeactivateEventParkingPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        var package = await _unitOfWork.EventParkingPackages.GetByIdWithSpaceAsync(command.PackageId, cancellationToken);
        if (package is null)
            return new ApiResponse<bool>(false, "Event package not found", false);

        var parking = package.ParkingSpace
            ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(package.ParkingSpaceId, cancellationToken);
        if (parking is null)
            return new ApiResponse<bool>(false, "Parking space not found", false);
        if (!command.IsAdmin && parking.OwnerId != command.ActorUserId)
            return new ApiResponse<bool>(false, "Unauthorized", false);

        package.Deactivate();
        _unitOfWork.EventParkingPackages.Update(package);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ApiResponse<bool>(true, "Event package deactivated", true);
    }
}

internal sealed class PurchaseEventParkingPackageHandler
    : ICommandHandler<PurchaseEventParkingPackageCommand, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IBookingAvailabilityService _availability;
    private readonly ICacheService _cache;
    private readonly ILogger<PurchaseEventParkingPackageHandler> _logger;

    public PurchaseEventParkingPackageHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IBookingAvailabilityService availability,
        ICacheService cache,
        ILogger<PurchaseEventParkingPackageHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _availability = availability;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(
        PurchaseEventParkingPackageCommand command,
        CancellationToken cancellationToken = default)
    {
        var package = await _unitOfWork.EventParkingPackages.GetByIdWithSpaceAsync(command.PackageId, cancellationToken);
        if (package is null)
            return new ApiResponse<BookingDto>(false, "Event package not found", null);

        var parking = package.ParkingSpace
            ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(package.ParkingSpaceId, cancellationToken);
        if (parking is null || !parking.IsActive)
            return new ApiResponse<BookingDto>(false, "Parking facility is not available", null);

        var now = DateTime.UtcNow;
        if (!package.IsOnSale(now))
            return new ApiResponse<BookingDto>(false, "This event package is not available for purchase", null);

        var availability = await _availability.CanCreateAsync(
            command.UserId,
            parking,
            package.AccessStartUtc,
            package.AccessEndUtc,
            slotNumber: null,
            command.VehicleNumber,
            cancellationToken);

        if (!availability.IsAllowed)
            return new ApiResponse<BookingDto>(false, availability.ErrorMessage ?? "Booking not available", null);

        if (!package.TryReserveSale(now))
            return new ApiResponse<BookingDto>(false, "This event package is sold out", null);

        var (tax, service, total) = EventPackageMapper.PricePackage(package.PackagePrice);

        try
        {
            var booking = Booking.CreateFromEventPackage(
                command.UserId,
                package,
                command.VehicleType,
                tax,
                service,
                total,
                command.VehicleNumber,
                command.VehicleModel,
                command.VehicleColor);

            await _unitOfWork.Bookings.AddAsync(booking, cancellationToken);
            _unitOfWork.EventParkingPackages.Update(package);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await CacheInvalidation.ForBookingChangeAsync(
                _cache,
                package.ParkingSpaceId,
                memberId: booking.UserId,
                vendorId: parking.OwnerId,
                cancellationToken);

            _logger.LogInformation(
                "Event package {PackageId} purchased as booking {BookingId}",
                package.Id,
                booking.Id);

            var message = total > 0
                ? "Event package reserved — complete payment to confirm"
                : "Event package booking confirmed";

            return new ApiResponse<BookingDto>(true, message, booking.ToDto());
        }
        catch (DomainException ex)
        {
            package.ReleaseSale();
            _unitOfWork.EventParkingPackages.Update(package);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<BookingDto>(false, ex.Message, null);
        }
    }
}
