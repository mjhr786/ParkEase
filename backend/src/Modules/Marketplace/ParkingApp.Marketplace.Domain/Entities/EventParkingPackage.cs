using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;

namespace ParkingApp.Marketplace.Domain.Entities;

/// <summary>
/// Prepaid event parking package for a facility (ParkWhiz-style fixed window + flat price).
/// Phase 2: multi-lot venue zones via <see cref="VenueEventId"/>, entry/exit buffers.
/// </summary>
public class EventParkingPackage : BaseEntity
{
    public Guid ParkingSpaceId { get; internal set; }
    public Guid CreatedByUserId { get; internal set; }

    /// <summary>
    /// Groups packages across lots for the same event (multi-zone).
    /// Defaults to a new id per package when the vendor does not reuse one.
    /// </summary>
    public Guid VenueEventId { get; internal set; }

    public string Title { get; internal set; } = string.Empty;
    public string? Description { get; internal set; }
    public string? EventName { get; internal set; }
    public string? VenueName { get; internal set; }

    /// <summary>Optional zone label within a venue event (e.g. "VIP Garage").</summary>
    public string? ZoneName { get; internal set; }

    /// <summary>Event showtime start (UTC). Parking may open earlier via EarlyEntryMinutes.</summary>
    public DateTime EventStartUtc { get; internal set; }

    /// <summary>Event showtime end (UTC). Parking may close later via LateExitMinutes.</summary>
    public DateTime EventEndUtc { get; internal set; }

    /// <summary>Minutes before EventStartUtc that parking access opens.</summary>
    public int EarlyEntryMinutes { get; internal set; }

    /// <summary>Minutes after EventEndUtc that parking access remains valid.</summary>
    public int LateExitMinutes { get; internal set; }

    /// <summary>When sales open (UTC). Default = creation time.</summary>
    public DateTime SalesStartUtc { get; internal set; }

    /// <summary>Optional sales cutoff; null = until event start.</summary>
    public DateTime? SalesEndUtc { get; internal set; }

    /// <summary>Flat package base price (tax/service applied at purchase).</summary>
    public decimal PackagePrice { get; internal set; }

    /// <summary>Max packages that can be sold.</summary>
    public int TotalSpots { get; internal set; }

    public int SoldCount { get; internal set; }
    public bool IsActive { get; internal set; } = true;

    public virtual ParkingSpace? ParkingSpace { get; internal set; }

    public int AvailableSpots => Math.Max(0, TotalSpots - SoldCount);

    /// <summary>Parking access window start (showtime − early entry).</summary>
    public DateTime AccessStartUtc => EventStartUtc.AddMinutes(-EarlyEntryMinutes);

    /// <summary>Parking access window end (showtime + late exit).</summary>
    public DateTime AccessEndUtc => EventEndUtc.AddMinutes(LateExitMinutes);

    internal EventParkingPackage()
    {
    }

    public static EventParkingPackage Create(
        Guid parkingSpaceId,
        Guid createdByUserId,
        string title,
        DateTime eventStartUtc,
        DateTime eventEndUtc,
        decimal packagePrice,
        int totalSpots,
        string? description = null,
        string? eventName = null,
        string? venueName = null,
        DateTime? salesStartUtc = null,
        DateTime? salesEndUtc = null,
        Guid? venueEventId = null,
        string? zoneName = null,
        int earlyEntryMinutes = 0,
        int lateExitMinutes = 0)
    {
        if (parkingSpaceId == Guid.Empty)
            throw new ValidationException("parkingSpaceId", "Parking space is required");
        if (createdByUserId == Guid.Empty)
            throw new ValidationException("createdByUserId", "Creator is required");
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("title", "Title is required");
        if (eventEndUtc <= eventStartUtc)
            throw new ValidationException("eventEndUtc", "Event end must be after event start");
        if (packagePrice < 0)
            throw new ValidationException("packagePrice", "Package price cannot be negative");
        if (totalSpots < 1)
            throw new ValidationException("totalSpots", "Total spots must be at least 1");
        if (earlyEntryMinutes < 0)
            throw new ValidationException("earlyEntryMinutes", "Early entry minutes cannot be negative");
        if (lateExitMinutes < 0)
            throw new ValidationException("lateExitMinutes", "Late exit minutes cannot be negative");
        if (earlyEntryMinutes > 24 * 60 || lateExitMinutes > 24 * 60)
            throw new ValidationException("buffers", "Entry/exit buffers cannot exceed 24 hours");

        var salesStart = salesStartUtc ?? DateTime.UtcNow;
        if (salesEndUtc.HasValue && salesEndUtc.Value <= salesStart)
            throw new ValidationException("salesEndUtc", "Sales end must be after sales start");

        return new EventParkingPackage
        {
            ParkingSpaceId = parkingSpaceId,
            CreatedByUserId = createdByUserId,
            VenueEventId = venueEventId is { } ve && ve != Guid.Empty ? ve : Guid.NewGuid(),
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            EventName = string.IsNullOrWhiteSpace(eventName) ? null : eventName.Trim(),
            VenueName = string.IsNullOrWhiteSpace(venueName) ? null : venueName.Trim(),
            ZoneName = string.IsNullOrWhiteSpace(zoneName) ? null : zoneName.Trim(),
            EventStartUtc = eventStartUtc,
            EventEndUtc = eventEndUtc,
            EarlyEntryMinutes = earlyEntryMinutes,
            LateExitMinutes = lateExitMinutes,
            SalesStartUtc = salesStart,
            SalesEndUtc = salesEndUtc,
            PackagePrice = Math.Round(packagePrice, 2, MidpointRounding.AwayFromZero),
            TotalSpots = totalSpots,
            SoldCount = 0,
            IsActive = true
        };
    }

    public void UpdateDetails(
        string? title = null,
        string? description = null,
        string? eventName = null,
        string? venueName = null,
        DateTime? eventStartUtc = null,
        DateTime? eventEndUtc = null,
        decimal? packagePrice = null,
        int? totalSpots = null,
        DateTime? salesStartUtc = null,
        DateTime? salesEndUtc = null,
        bool? isActive = null,
        Guid? venueEventId = null,
        string? zoneName = null,
        int? earlyEntryMinutes = null,
        int? lateExitMinutes = null)
    {
        if (!string.IsNullOrWhiteSpace(title))
            Title = title.Trim();
        if (description != null)
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (eventName != null)
            EventName = string.IsNullOrWhiteSpace(eventName) ? null : eventName.Trim();
        if (venueName != null)
            VenueName = string.IsNullOrWhiteSpace(venueName) ? null : venueName.Trim();
        if (zoneName != null)
            ZoneName = string.IsNullOrWhiteSpace(zoneName) ? null : zoneName.Trim();

        if (venueEventId.HasValue && venueEventId.Value != Guid.Empty)
            VenueEventId = venueEventId.Value;

        var start = eventStartUtc ?? EventStartUtc;
        var end = eventEndUtc ?? EventEndUtc;
        if (end <= start)
            throw new ValidationException("eventEndUtc", "Event end must be after event start");
        EventStartUtc = start;
        EventEndUtc = end;

        if (earlyEntryMinutes.HasValue)
        {
            if (earlyEntryMinutes.Value < 0)
                throw new ValidationException("earlyEntryMinutes", "Early entry minutes cannot be negative");
            if (earlyEntryMinutes.Value > 24 * 60)
                throw new ValidationException("earlyEntryMinutes", "Early entry cannot exceed 24 hours");
            EarlyEntryMinutes = earlyEntryMinutes.Value;
        }

        if (lateExitMinutes.HasValue)
        {
            if (lateExitMinutes.Value < 0)
                throw new ValidationException("lateExitMinutes", "Late exit minutes cannot be negative");
            if (lateExitMinutes.Value > 24 * 60)
                throw new ValidationException("lateExitMinutes", "Late exit cannot exceed 24 hours");
            LateExitMinutes = lateExitMinutes.Value;
        }

        if (packagePrice.HasValue)
        {
            if (packagePrice.Value < 0)
                throw new ValidationException("packagePrice", "Package price cannot be negative");
            PackagePrice = Math.Round(packagePrice.Value, 2, MidpointRounding.AwayFromZero);
        }

        if (totalSpots.HasValue)
        {
            if (totalSpots.Value < 1)
                throw new ValidationException("totalSpots", "Total spots must be at least 1");
            if (totalSpots.Value < SoldCount)
                throw new ValidationException("totalSpots", "Total spots cannot be less than already sold packages");
            TotalSpots = totalSpots.Value;
        }

        if (salesStartUtc.HasValue)
            SalesStartUtc = salesStartUtc.Value;
        if (salesEndUtc.HasValue)
        {
            if (salesEndUtc.Value <= SalesStartUtc)
                throw new ValidationException("salesEndUtc", "Sales end must be after sales start");
            SalesEndUtc = salesEndUtc;
        }

        if (isActive.HasValue)
            IsActive = isActive.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsOnSale(DateTime asOfUtc)
    {
        if (!IsActive || AvailableSpots < 1)
            return false;
        if (asOfUtc < SalesStartUtc)
            return false;
        var salesEnd = SalesEndUtc ?? EventStartUtc;
        if (asOfUtc > salesEnd)
            return false;
        // Stop selling after parking access ends
        if (asOfUtc >= AccessEndUtc)
            return false;
        return true;
    }

    /// <summary>Reserves one package unit. Returns false if sold out or not on sale.</summary>
    public bool TryReserveSale(DateTime asOfUtc)
    {
        if (!IsOnSale(asOfUtc))
            return false;
        SoldCount++;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public void ReleaseSale()
    {
        if (SoldCount <= 0) return;
        SoldCount--;
        UpdatedAt = DateTime.UtcNow;
    }
}
