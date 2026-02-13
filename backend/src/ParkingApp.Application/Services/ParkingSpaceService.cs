using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using ParkingApp.BuildingBlocks.Logging;

namespace ParkingApp.Application.Services;

public class ParkingSpaceService : IParkingSpaceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<ParkingSpaceService> _logger;
    private static readonly TimeSpan ParkingCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(2);

    public ParkingSpaceService(IUnitOfWork unitOfWork, ICacheService cache, ILogger<ParkingSpaceService> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<ParkingSpaceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Try cache first
        var cacheKey = $"parking:{id}";
        var cached = await _cache.GetAsync<ParkingSpaceDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogCacheHit(cacheKey);
            return new ApiResponse<ParkingSpaceDto>(true, null, cached);
        }

        _logger.LogCacheMiss(cacheKey);
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(id, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<ParkingSpaceDto>(false, "Parking space not found", null);
        }

        var dto = parking.ToDto();
        
        // Cache the result
        await _cache.SetAsync(cacheKey, dto, ParkingCacheDuration, cancellationToken);

        return new ApiResponse<ParkingSpaceDto>(true, null, dto);
    }

    public async Task<ApiResponse<ParkingSearchResultDto>> SearchAsync(ParkingSearchDto dto, CancellationToken cancellationToken = default)
    {
        // Create cache key from search parameters
        var cacheKey = $"search:{dto.State}:{dto.City}:{dto.Address}:{dto.ParkingType}:{dto.VehicleType}:{dto.MinPrice}:{dto.MaxPrice}:{dto.Page}:{dto.PageSize}";
        var cached = await _cache.GetAsync<ParkingSearchResultDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogCacheHit(cacheKey);
            return new ApiResponse<ParkingSearchResultDto>(true, null, cached);
        }

        _logger.LogInformation("Searching parking spaces: City={City}, Type={ParkingType}, Vehicle={VehicleType}", 
            dto.City, dto.ParkingType, dto.VehicleType);
        var parkingSpaces = await _unitOfWork.ParkingSpaces.SearchAsync(
            state: dto.State,
            city: dto.City,
            address: dto.Address,
            latitude: dto.Latitude,
            longitude: dto.Longitude,
            radiusKm: dto.RadiusKm,
            startDate: dto.StartDateTime,
            endDate: dto.EndDateTime,
            minPrice: dto.MinPrice,
            maxPrice: dto.MaxPrice,
            parkingType: dto.ParkingType?.ToString(),
            vehicleType: dto.VehicleType?.ToString(),
            amenities: dto.Amenities != null ? string.Join(",", dto.Amenities) : null,
            minRating: dto.MinRating,
            page: dto.Page,
            pageSize: dto.PageSize,
            cancellationToken: cancellationToken
        );

        var parkingList = parkingSpaces.ToList();
        var totalCount = await _unitOfWork.ParkingSpaces.CountAsync(p => p.IsActive, cancellationToken);

        // Fetch active bookings for each parking space to show reservation periods
        var parkingDtos = new List<ParkingSpaceDto>();
        foreach (var parking in parkingList)
        {
            var bookings = await _unitOfWork.Bookings.GetByParkingSpaceIdAsync(parking.Id, cancellationToken);
            parkingDtos.Add(parking.ToDtoWithReservations(bookings));
        }

        var result = new ParkingSearchResultDto(
            parkingDtos,
            totalCount,
            dto.Page,
            dto.PageSize,
            (int)Math.Ceiling((double)totalCount / dto.PageSize)
        );

        // Cache the search result
        await _cache.SetAsync(cacheKey, result, SearchCacheDuration, cancellationToken);

        return new ApiResponse<ParkingSearchResultDto>(true, null, result);
    }

    public async Task<ApiResponse<List<ParkingSpaceDto>>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var parkingSpaces = await _unitOfWork.ParkingSpaces.GetByOwnerIdAsync(ownerId, cancellationToken);
        var dtos = parkingSpaces.Select(p => p.ToDto()).ToList();

        return new ApiResponse<List<ParkingSpaceDto>>(true, null, dtos);
    }

    public async Task<ApiResponse<ParkingSpaceDto>> CreateAsync(Guid ownerId, CreateParkingSpaceDto dto, CancellationToken cancellationToken = default)
    {
        // Verify owner exists and is a vendor
        var owner = await _unitOfWork.Users.GetByIdAsync(ownerId, cancellationToken);
        if (owner == null)
        {
            return new ApiResponse<ParkingSpaceDto>(false, "Owner not found", null);
        }

        if (owner.Role != UserRole.Vendor && owner.Role != UserRole.Admin)
        {
            return new ApiResponse<ParkingSpaceDto>(false, "Only vendors can create parking spaces", null);
        }

        var parking = dto.ToEntity(ownerId);
        parking.Owner = owner;

        await _unitOfWork.ParkingSpaces.AddAsync(parking, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate search cache (new parking added)
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);
        
        _logger.LogEntityCreated<ParkingSpace>(parking.Id);

        return new ApiResponse<ParkingSpaceDto>(true, "Parking space created", parking.ToDto());
    }

    public async Task<ApiResponse<ParkingSpaceDto>> UpdateAsync(Guid id, Guid ownerId, UpdateParkingSpaceDto dto, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(id, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<ParkingSpaceDto>(false, "Parking space not found", null);
        }

        if (parking.OwnerId != ownerId)
        {
            return new ApiResponse<ParkingSpaceDto>(false, "Unauthorized", null);
        }

        // Update only provided fields
        if (!string.IsNullOrEmpty(dto.Title)) parking.Title = dto.Title;
        if (!string.IsNullOrEmpty(dto.Description)) parking.Description = dto.Description;
        if (!string.IsNullOrEmpty(dto.Address)) parking.Address = dto.Address;
        if (!string.IsNullOrEmpty(dto.City)) parking.City = dto.City;
        if (!string.IsNullOrEmpty(dto.State)) parking.State = dto.State;
        if (!string.IsNullOrEmpty(dto.Country)) parking.Country = dto.Country;
        if (!string.IsNullOrEmpty(dto.PostalCode)) parking.PostalCode = dto.PostalCode;
        if (dto.Latitude.HasValue) parking.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) parking.Longitude = dto.Longitude.Value;
        
        // Update PostGIS Location point when Lat/Lon change
        if (dto.Latitude.HasValue || dto.Longitude.HasValue)
        {
            parking.Location = new NetTopologySuite.Geometries.Point(parking.Longitude, parking.Latitude) { SRID = 4326 };
        }
        
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

        _unitOfWork.ParkingSpaces.Update(parking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate caches
        await _cache.RemoveAsync($"parking:{id}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);
        
        _logger.LogEntityUpdated<ParkingSpace>(id);

        return new ApiResponse<ParkingSpaceDto>(true, "Parking space updated", parking.ToDto());
    }

    public async Task<ApiResponse<bool>> DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(id, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<bool>(false, "Parking space not found", false);
        }

        if (parking.OwnerId != ownerId)
        {
            return new ApiResponse<bool>(false, "Unauthorized", false);
        }

        // Check for active bookings
        var hasActiveBookings = await _unitOfWork.Bookings.AnyAsync(b =>
            b.ParkingSpaceId == id &&
            (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.InProgress),
            cancellationToken);

        if (hasActiveBookings)
        {
            return new ApiResponse<bool>(false, "Cannot delete parking space with active bookings", false);
        }

        _unitOfWork.ParkingSpaces.Remove(parking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate caches
        await _cache.RemoveAsync($"parking:{id}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);
        
        _logger.LogEntityDeleted<ParkingSpace>(id);

        return new ApiResponse<bool>(true, "Parking space deleted", true);
    }

    public async Task<ApiResponse<bool>> ToggleActiveAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(id, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<bool>(false, "Parking space not found", false);
        }

        if (parking.OwnerId != ownerId)
        {
            return new ApiResponse<bool>(false, "Unauthorized", false);
        }

        parking.IsActive = !parking.IsActive;
        _unitOfWork.ParkingSpaces.Update(parking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate caches
        await _cache.RemoveAsync($"parking:{id}", cancellationToken);
        await _cache.RemoveByPatternAsync("search:*", cancellationToken);
        
        _logger.LogCacheInvalidated($"parking:{id}");

        return new ApiResponse<bool>(true, $"Parking space {(parking.IsActive ? "activated" : "deactivated")}", true);
    }
}
