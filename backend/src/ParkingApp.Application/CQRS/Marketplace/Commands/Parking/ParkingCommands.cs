using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Application.CQRS.Commands.Parking;

// ────────────────────────────────────────────────────────────────
// Commands (Data contracts)
// ────────────────────────────────────────────────────────────────

public sealed record CreateParkingCommand(Guid OwnerId, CreateParkingSpaceDto Dto) : ICommand<ApiResponse<ParkingSpaceDto>>;
public sealed record UpdateParkingCommand(Guid ParkingId, Guid OwnerId, UpdateParkingSpaceDto Dto) : ICommand<ApiResponse<ParkingSpaceDto>>;
public sealed record DeleteParkingCommand(Guid ParkingId, Guid OwnerId) : ICommand<ApiResponse<bool>>;
public sealed record ToggleActiveParkingCommand(Guid ParkingId, Guid OwnerId) : ICommand<ApiResponse<bool>>;

// ────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────

public sealed class CreateParkingHandler : ICommandHandler<CreateParkingCommand, ApiResponse<ParkingSpaceDto>>
{
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly IIdentityUnitOfWork _identity;
    private readonly ILogger<CreateParkingHandler> _logger;

    public CreateParkingHandler(IMarketplaceUnitOfWork marketplace, IIdentityUnitOfWork identity, ILogger<CreateParkingHandler> logger)
    {
        _marketplace = marketplace;
        _identity = identity;
        _logger = logger;
    }

    public async Task<ApiResponse<ParkingSpaceDto>> HandleAsync(CreateParkingCommand command, CancellationToken cancellationToken = default)
    {
        var owner = await _identity.Users.GetByIdAsync(command.OwnerId, cancellationToken);
        if (owner == null)
            return new ApiResponse<ParkingSpaceDto>(false, "Owner not found", null);

        try
        {
            // Factory raises ParkingSpaceCreatedEvent → cache handler after SaveChanges
            var parking = command.Dto.ToEntity(command.OwnerId);
            parking.AssignOwnerNavigation(owner);

            await _marketplace.ParkingSpaces.AddAsync(parking, cancellationToken);
            await _marketplace.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Parking space {Id} created by owner {OwnerId}", parking.Id, command.OwnerId);
            return new ApiResponse<ParkingSpaceDto>(true, "Parking space created", parking.ToDto());
        }
        catch (DomainException ex)
        {
            return new ApiResponse<ParkingSpaceDto>(false, ex.Message, null);
        }
    }
}

public sealed class UpdateParkingHandler : ICommandHandler<UpdateParkingCommand, ApiResponse<ParkingSpaceDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateParkingHandler> _logger;

    public UpdateParkingHandler(IMarketplaceUnitOfWork unitOfWork, ILogger<UpdateParkingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<ParkingSpaceDto>> HandleAsync(UpdateParkingCommand command, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingId, cancellationToken);
        if (parking == null)
            return new ApiResponse<ParkingSpaceDto>(false, "Parking space not found", null);

        if (parking.OwnerId != command.OwnerId)
            return new ApiResponse<ParkingSpaceDto>(false, "Unauthorized", null);

        var dto = command.Dto;

        try
        {
            parking.UpdateDetails(
                title: dto.Title,
                description: dto.Description,
                address: dto.Address,
                city: dto.City,
                state: dto.State,
                country: dto.Country,
                postalCode: dto.PostalCode,
                zoneCode: dto.ZoneCode,
                latitude: dto.Latitude,
                longitude: dto.Longitude,
                parkingType: dto.ParkingType,
                totalSpots: dto.TotalSpots,
                hourlyRate: dto.HourlyRate,
                dailyRate: dto.DailyRate,
                weeklyRate: dto.WeeklyRate,
                monthlyRate: dto.MonthlyRate,
                openTime: dto.OpenTime,
                closeTime: dto.CloseTime,
                is24Hours: dto.Is24Hours,
                amenities: dto.Amenities,
                allowedVehicleTypes: dto.AllowedVehicleTypes?.Select(v => v.ToString()),
                imageUrls: dto.ImageUrls,
                specialInstructions: dto.SpecialInstructions,
                isActive: dto.IsActive);

            _unitOfWork.ParkingSpaces.Update(parking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Parking space {Id} updated", command.ParkingId);
            return new ApiResponse<ParkingSpaceDto>(true, "Parking space updated", parking.ToDto());
        }
        catch (DomainException ex)
        {
            return new ApiResponse<ParkingSpaceDto>(false, ex.Message, null);
        }
    }
}

public sealed class DeleteParkingHandler : ICommandHandler<DeleteParkingCommand, ApiResponse<bool>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteParkingHandler> _logger;

    public DeleteParkingHandler(IMarketplaceUnitOfWork unitOfWork, ILogger<DeleteParkingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> HandleAsync(DeleteParkingCommand command, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingId, cancellationToken);
        if (parking == null)
            return new ApiResponse<bool>(false, "Parking space not found", false);

        if (parking.OwnerId != command.OwnerId)
            return new ApiResponse<bool>(false, "Unauthorized", false);

        var hasActiveBookings = await _unitOfWork.Bookings.HasBlockingBookingsForSpaceAsync(
            command.ParkingId, DateTime.UtcNow, cancellationToken);

        if (hasActiveBookings)
            return new ApiResponse<bool>(false, "Cannot delete parking space with active bookings", false);

        // Domain raises ParkingSpaceDeletedEvent; soft-delete flags set on aggregate
        parking.Retire(command.OwnerId);
        _unitOfWork.ParkingSpaces.Update(parking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Parking space {Id} deleted", command.ParkingId);
        return new ApiResponse<bool>(true, "Parking space deleted", true);
    }
}

public sealed class ToggleActiveParkingHandler : ICommandHandler<ToggleActiveParkingCommand, ApiResponse<bool>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly ILogger<ToggleActiveParkingHandler> _logger;

    public ToggleActiveParkingHandler(IMarketplaceUnitOfWork unitOfWork, ILogger<ToggleActiveParkingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> HandleAsync(ToggleActiveParkingCommand command, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingId, cancellationToken);
        if (parking == null)
            return new ApiResponse<bool>(false, "Parking space not found", false);

        if (parking.OwnerId != command.OwnerId)
            return new ApiResponse<bool>(false, "Unauthorized", false);

        parking.ToggleActive();
        _unitOfWork.ParkingSpaces.Update(parking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Parking space {Id} toggled to {IsActive}", command.ParkingId, parking.IsActive);
        return new ApiResponse<bool>(true, $"Parking space {(parking.IsActive ? "activated" : "deactivated")}", true);
    }
}
