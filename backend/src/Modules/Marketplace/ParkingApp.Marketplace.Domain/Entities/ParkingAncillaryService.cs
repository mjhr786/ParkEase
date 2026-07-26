using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;

namespace ParkingApp.Marketplace.Domain.Entities;

/// <summary>
/// Vendor catalog item for checkout add-ons (car wash, detailing, etc.) on a parking space.
/// </summary>
public class ParkingAncillaryService : BaseEntity
{
    public Guid ParkingSpaceId { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public string? Description { get; internal set; }
    public decimal Price { get; internal set; }
    public int? DurationMinutes { get; internal set; }
    public bool IsActive { get; internal set; } = true;
    public int SortOrder { get; internal set; }

    public virtual ParkingSpace? ParkingSpace { get; internal set; }

    internal ParkingAncillaryService()
    {
    }

    public static ParkingAncillaryService Create(
        Guid parkingSpaceId,
        string name,
        decimal price,
        string? description = null,
        int? durationMinutes = null,
        int sortOrder = 0,
        bool isActive = true)
    {
        if (parkingSpaceId == Guid.Empty)
            throw new ValidationException("parkingSpaceId", "Parking space is required");
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("name", "Name is required");
        if (price < 0)
            throw new ValidationException("price", "Price cannot be negative");
        if (durationMinutes is < 0)
            throw new ValidationException("durationMinutes", "Duration cannot be negative");

        return new ParkingAncillaryService
        {
            ParkingSpaceId = parkingSpaceId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Price = Math.Round(price, 2, MidpointRounding.AwayFromZero),
            DurationMinutes = durationMinutes,
            SortOrder = sortOrder,
            IsActive = isActive
        };
    }

    public void Update(
        string? name = null,
        string? description = null,
        decimal? price = null,
        int? durationMinutes = null,
        bool? isActive = null,
        int? sortOrder = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Name = name.Trim();

        if (description != null)
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (price.HasValue)
        {
            if (price.Value < 0)
                throw new ValidationException("price", "Price cannot be negative");
            Price = Math.Round(price.Value, 2, MidpointRounding.AwayFromZero);
        }

        if (durationMinutes.HasValue)
        {
            if (durationMinutes.Value < 0)
                throw new ValidationException("durationMinutes", "Duration cannot be negative");
            DurationMinutes = durationMinutes.Value == 0 ? null : durationMinutes;
        }

        if (isActive.HasValue)
            IsActive = isActive.Value;

        if (sortOrder.HasValue)
            SortOrder = sortOrder.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
