using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Events.Parking;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<CreateParkingHandler> _logger;

    public CreateParkingHandler(IUnitOfWork unitOfWork, ICacheService cache, ILogger<CreateParkingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<ParkingSpaceDto>> HandleAsync(CreateParkingCommand command, CancellationToken cancellationToken = default)
    {
        var owner = await _unitOfWork.Users.GetByIdAsync(command.OwnerId, cancellationToken);
        if (owner == null)
            return new ApiResponse<ParkingSpaceDto>(false, "Owner not found", null);

        if (owner.Role != Domain.Enums.UserRole.Vendor && owner.Role != Domain.Enums.UserRole.Admin)
            return new ApiResponse<ParkingSpaceDto>(false, "Only vendors can create parking spaces", null);

        var parking = command.Dto.ToEntity(command.OwnerId);
        parking.Owner = owner;

        // Raise domain event
        parking.AddDomainEvent(new ParkingSpaceCreatedEvent(parking.Id, command.OwnerId, parking.Title));

        await _unitOfWork.ParkingSpaces.AddAsync(parking, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate search & map caches
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);
        await _cache.RemoveByPatternAsync("map:*", cancellationToken);

        _logger.LogInformation("Parking space {Id} created by owner {OwnerId}", parking.Id, command.OwnerId);

        return new ApiResponse<ParkingSpaceDto>(true, "Parking space created", parking.ToDto());
    }
}

public sealed class UpdateParkingHandler : ICommandHandler<UpdateParkingCommand, ApiResponse<ParkingSpaceDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<UpdateParkingHandler> _logger;

    public UpdateParkingHandler(IUnitOfWork unitOfWork, ICacheService cache, ILogger<UpdateParkingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
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

        // Apply partial updates
        if (!string.IsNullOrEmpty(dto.Title)) parking.Title = dto.Title;
        if (!string.IsNullOrEmpty(dto.Description)) parking.Description = dto.Description;
        if (!string.IsNullOrEmpty(dto.Address)) parking.Address = dto.Address;
        if (!string.IsNullOrEmpty(dto.City)) parking.City = dto.City;
        if (!string.IsNullOrEmpty(dto.State)) parking.State = dto.State;
        if (!string.IsNullOrEmpty(dto.Country)) parking.Country = dto.Country;
        if (!string.IsNullOrEmpty(dto.PostalCode)) parking.PostalCode = dto.PostalCode;
        if (dto.Latitude.HasValue) parking.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) parking.Longitude = dto.Longitude.Value;

        if (dto.Latitude.HasValue || dto.Longitude.HasValue)
            parking.Location = new NetTopologySuite.Geometries.Point(parking.Longitude, parking.Latitude) { SRID = 4326 };

        if (dto.ParkingType.HasValue) parking.ParkingType = dto.ParkingType.Value;
        if (dto.TotalSpots.HasValue) parking.TotalSpots = dto.TotalSpots.Value;
        if (dto.HourlyRate.HasValue) parking.HourlyRate = dto.HourlyRate.Value;
        if (dto.DailyRate.HasValue) parking.DailyRate = dto.DailyRate.Value;
        if (dto.WeeklyRate.HasValue) parking.WeeklyRate = dto.WeeklyRate.Value;
        if (dto.MonthlyRate.HasValue) parking.MonthlyRate = dto.MonthlyRate.Value;
        if (dto.OpenTime.HasValue) parking.OpenTime = dto.OpenTime.Value;
        if (dto.CloseTime.HasValue) parking.CloseTime = dto.CloseTime.Value;
        if (dto.Is24Hours.HasValue) parking.Is24Hours = dto.Is24Hours.Value;
        if (dto.Amenities != null) parking.Amenities = string.Join(",", dto.Amenities);
        if (dto.AllowedVehicleTypes != null) parking.AllowedVehicleTypes = string.Join(",", dto.AllowedVehicleTypes);
        if (dto.ImageUrls != null) parking.ImageUrls = string.Join(",", dto.ImageUrls);
        if (dto.SpecialInstructions != null) parking.SpecialInstructions = dto.SpecialInstructions;
        if (dto.IsActive.HasValue) parking.IsActive = dto.IsActive.Value;

        parking.UpdatedAt = DateTime.UtcNow;

        // Raise domain event
        parking.AddDomainEvent(new ParkingSpaceUpdatedEvent(parking.Id, parking.Title));

        _unitOfWork.ParkingSpaces.Update(parking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync($"parking:{command.ParkingId}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);
        await _cache.RemoveByPatternAsync("map:*", cancellationToken);

        _logger.LogInformation("Parking space {Id} updated", command.ParkingId);

        return new ApiResponse<ParkingSpaceDto>(true, "Parking space updated", parking.ToDto());
    }
}

public sealed class DeleteParkingHandler : ICommandHandler<DeleteParkingCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<DeleteParkingHandler> _logger;

    public DeleteParkingHandler(IUnitOfWork unitOfWork, ICacheService cache, ILogger<DeleteParkingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> HandleAsync(DeleteParkingCommand command, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingId, cancellationToken);
        if (parking == null)
            return new ApiResponse<bool>(false, "Parking space not found", false);

        if (parking.OwnerId != command.OwnerId)
            return new ApiResponse<bool>(false, "Unauthorized", false);

        var hasActiveBookings = await _unitOfWork.Bookings.AnyAsync(b =>
            b.ParkingSpaceId == command.ParkingId &&
            (b.Status == Domain.Enums.BookingStatus.Confirmed || b.Status == Domain.Enums.BookingStatus.InProgress),
            cancellationToken);

        if (hasActiveBookings)
            return new ApiResponse<bool>(false, "Cannot delete parking space with active bookings", false);

        parking.AddDomainEvent(new ParkingSpaceDeletedEvent(parking.Id, command.OwnerId));

        _unitOfWork.ParkingSpaces.Remove(parking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync($"parking:{command.ParkingId}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);
        await _cache.RemoveByPatternAsync("map:*", cancellationToken);

        _logger.LogInformation("Parking space {Id} deleted", command.ParkingId);

        return new ApiResponse<bool>(true, "Parking space deleted", true);
    }
}

public sealed class ToggleActiveParkingHandler : ICommandHandler<ToggleActiveParkingCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<ToggleActiveParkingHandler> _logger;

    public ToggleActiveParkingHandler(IUnitOfWork unitOfWork, ICacheService cache, ILogger<ToggleActiveParkingHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> HandleAsync(ToggleActiveParkingCommand command, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingId, cancellationToken);
        if (parking == null)
            return new ApiResponse<bool>(false, "Parking space not found", false);

        if (parking.OwnerId != command.OwnerId)
            return new ApiResponse<bool>(false, "Unauthorized", false);

        parking.IsActive = !parking.IsActive;
        parking.UpdatedAt = DateTime.UtcNow;

        parking.AddDomainEvent(new ParkingSpaceToggledEvent(parking.Id, parking.IsActive));

        _unitOfWork.ParkingSpaces.Update(parking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync($"parking:{command.ParkingId}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);
        await _cache.RemoveByPatternAsync("map:*", cancellationToken);

        _logger.LogInformation("Parking space {Id} toggled to {IsActive}", command.ParkingId, parking.IsActive);

        return new ApiResponse<bool>(true, $"Parking space {(parking.IsActive ? "activated" : "deactivated")}", true);
    }
}
