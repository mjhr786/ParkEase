using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Shared;
using NetTopologySuite.Geometries;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Events.Parking;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Domain.Marketplace;

/// <summary>
/// Parking space aggregate root (marketplace vendor lots + company-owned lots).
/// Create via factories; mutate only through domain methods.
/// </summary>
public class ParkingSpace : BaseEntity
{
    // internal set: Application cannot free-mutate; tests/EF via InternalsVisibleTo + reflection
    public Guid OwnerId { get; internal set; }
    public Guid? CompanyOwnerId { get; internal set; }
    public ParkingSpaceOwnershipType OwnershipType { get; internal set; } = ParkingSpaceOwnershipType.IndividualVendor;
    public bool IsCorporateOnly { get; internal set; }
    public string Title { get; internal set; } = string.Empty;
    public string Description { get; internal set; } = string.Empty;

    public string Address { get; internal set; } = string.Empty;
    public string City { get; internal set; } = string.Empty;
    public string State { get; internal set; } = string.Empty;
    public string Country { get; internal set; } = string.Empty;
    public string PostalCode { get; internal set; } = string.Empty;
    public string? ZoneCode { get; internal set; }
    public double Latitude { get; internal set; }
    public double Longitude { get; internal set; }
    public Point? Location { get; internal set; }

    public ParkingType ParkingType { get; internal set; }
    public int TotalSpots { get; internal set; } = 1;
    public int AvailableSpots { get; internal set; } = 1;

    public decimal HourlyRate { get; internal set; }
    public decimal DailyRate { get; internal set; }
    public decimal WeeklyRate { get; internal set; }
    public decimal MonthlyRate { get; internal set; }

    public TimeSpan OpenTime { get; internal set; } = TimeSpan.Zero;
    public TimeSpan CloseTime { get; internal set; } = new TimeSpan(23, 59, 59);
    public bool Is24Hours { get; internal set; } = true;
    public string? AvailableDays { get; internal set; } = "1,2,3,4,5,6,7";

    public string? Amenities { get; internal set; }
    public string? AllowedVehicleTypes { get; internal set; }
    public string? ImageUrls { get; internal set; }

    public bool IsActive { get; internal set; } = true;
    public bool IsVerified { get; internal set; }

    public double AverageRating { get; internal set; }
    public int TotalReviews { get; internal set; }

    public string? SpecialInstructions { get; internal set; }

    public virtual User Owner { get; internal set; } = null!;
    public virtual Corporate.Company? CompanyOwner { get; internal set; }
    public virtual ICollection<Booking> Bookings { get; internal set; } = new List<Booking>();
    public virtual ICollection<Review> Reviews { get; internal set; } = new List<Review>();
    public virtual ICollection<ParkingAvailability> Availabilities { get; internal set; } = new List<ParkingAvailability>();
    public virtual ICollection<Favorite> FavoritedBy { get; internal set; } = new List<Favorite>();
    public virtual ICollection<ParkingPass> ParkingPasses { get; internal set; } = new List<ParkingPass>();

    internal ParkingSpace()
    {
    }

    // ── Factories ─────────────────────────────────────────────────

    public static ParkingSpace CreateForVendor(
        Guid ownerId,
        string title,
        string description,
        string address,
        string city,
        string state,
        string country,
        string postalCode,
        double latitude,
        double longitude,
        ParkingType parkingType,
        int totalSpots,
        decimal hourlyRate,
        decimal dailyRate,
        decimal weeklyRate,
        decimal monthlyRate,
        TimeSpan? openTime = null,
        TimeSpan? closeTime = null,
        bool is24Hours = true,
        IEnumerable<string>? amenities = null,
        IEnumerable<string>? allowedVehicleTypes = null,
        IEnumerable<string>? imageUrls = null,
        string? specialInstructions = null,
        string? zoneCode = null)
    {
        if (ownerId == Guid.Empty)
            throw new ValidationException("ownerId", "Owner ID is required");

        var parking = CreateCore(
            ownerId,
            title,
            description,
            address,
            city,
            state,
            country,
            postalCode,
            latitude,
            longitude,
            parkingType,
            totalSpots,
            hourlyRate,
            dailyRate,
            weeklyRate,
            monthlyRate,
            openTime,
            closeTime,
            is24Hours,
            amenities,
            allowedVehicleTypes,
            imageUrls,
            specialInstructions,
            zoneCode);

        parking.OwnershipType = ParkingSpaceOwnershipType.IndividualVendor;
        parking.IsCorporateOnly = false;
        parking.AddDomainEvent(new ParkingSpaceCreatedEvent(parking.Id, ownerId, parking.Title));
        return parking;
    }

    public static ParkingSpace CreateForCompany(
        Guid adminUserId,
        Guid companyId,
        string title,
        string description,
        string address,
        string city,
        string state,
        string country,
        string postalCode,
        double latitude,
        double longitude,
        ParkingType parkingType,
        int totalSpots,
        decimal hourlyRate,
        decimal dailyRate,
        decimal weeklyRate,
        decimal monthlyRate,
        TimeSpan? openTime = null,
        TimeSpan? closeTime = null,
        bool is24Hours = true,
        IEnumerable<string>? amenities = null,
        IEnumerable<string>? allowedVehicleTypes = null,
        IEnumerable<string>? imageUrls = null,
        string? specialInstructions = null,
        string? zoneCode = null)
    {
        if (adminUserId == Guid.Empty)
            throw new ValidationException("adminUserId", "Admin user ID is required");
        if (companyId == Guid.Empty)
            throw new ValidationException("companyId", "Company ID is required");

        var parking = CreateCore(
            adminUserId,
            title,
            description,
            address,
            city,
            state,
            country,
            postalCode,
            latitude,
            longitude,
            parkingType,
            totalSpots,
            hourlyRate,
            dailyRate,
            weeklyRate,
            monthlyRate,
            openTime,
            closeTime,
            is24Hours,
            amenities,
            allowedVehicleTypes,
            imageUrls,
            specialInstructions,
            zoneCode);

        parking.CompanyOwnerId = companyId;
        parking.OwnershipType = ParkingSpaceOwnershipType.CompanyOwned;
        parking.IsCorporateOnly = true;
        parking.IsVerified = true;
        parking.AddDomainEvent(new ParkingSpaceCreatedEvent(parking.Id, adminUserId, parking.Title));
        return parking;
    }

    // ── Updates ───────────────────────────────────────────────────

    public void UpdateDetails(
        string? title = null,
        string? description = null,
        string? address = null,
        string? city = null,
        string? state = null,
        string? country = null,
        string? postalCode = null,
        string? zoneCode = null,
        double? latitude = null,
        double? longitude = null,
        ParkingType? parkingType = null,
        int? totalSpots = null,
        decimal? hourlyRate = null,
        decimal? dailyRate = null,
        decimal? weeklyRate = null,
        decimal? monthlyRate = null,
        TimeSpan? openTime = null,
        TimeSpan? closeTime = null,
        bool? is24Hours = null,
        IEnumerable<string>? amenities = null,
        IEnumerable<string>? allowedVehicleTypes = null,
        IEnumerable<string>? imageUrls = null,
        string? specialInstructions = null,
        bool? isActive = null,
        bool raiseUpdatedEvent = true)
    {
        if (!string.IsNullOrWhiteSpace(title)) Title = title.Trim();
        if (!string.IsNullOrWhiteSpace(description)) Description = description.Trim();
        if (!string.IsNullOrWhiteSpace(address)) Address = address.Trim();
        if (!string.IsNullOrWhiteSpace(city)) City = city.Trim();
        if (!string.IsNullOrWhiteSpace(state)) State = state.Trim();
        if (!string.IsNullOrWhiteSpace(country)) Country = country.Trim();
        if (!string.IsNullOrWhiteSpace(postalCode)) PostalCode = postalCode.Trim();

        if (zoneCode != null)
            ZoneCode = string.IsNullOrWhiteSpace(zoneCode) ? null : zoneCode.Trim().ToUpperInvariant();

        if (latitude.HasValue) Latitude = latitude.Value;
        if (longitude.HasValue) Longitude = longitude.Value;
        if (latitude.HasValue || longitude.HasValue)
            SyncLocationFromCoordinates();

        if (parkingType.HasValue) ParkingType = parkingType.Value;

        if (totalSpots.HasValue)
        {
            if (totalSpots.Value < 1)
                throw new ValidationException("totalSpots", "Total spots must be at least 1");
            TotalSpots = totalSpots.Value;
            AvailableSpots = Math.Min(AvailableSpots, TotalSpots);
            if (AvailableSpots < 1)
                AvailableSpots = TotalSpots;
        }

        if (hourlyRate.HasValue) HourlyRate = RequireNonNegative(hourlyRate.Value, nameof(hourlyRate));
        if (dailyRate.HasValue) DailyRate = RequireNonNegative(dailyRate.Value, nameof(dailyRate));
        if (weeklyRate.HasValue) WeeklyRate = RequireNonNegative(weeklyRate.Value, nameof(weeklyRate));
        if (monthlyRate.HasValue) MonthlyRate = RequireNonNegative(monthlyRate.Value, nameof(monthlyRate));

        if (openTime.HasValue) OpenTime = openTime.Value;
        if (closeTime.HasValue) CloseTime = closeTime.Value;
        if (is24Hours.HasValue) Is24Hours = is24Hours.Value;

        if (amenities != null) Amenities = JoinCsv(amenities);
        if (allowedVehicleTypes != null) AllowedVehicleTypes = JoinCsv(allowedVehicleTypes);
        if (imageUrls != null) ImageUrls = JoinCsv(imageUrls);
        if (specialInstructions != null) SpecialInstructions = string.IsNullOrWhiteSpace(specialInstructions) ? null : specialInstructions.Trim();

        if (isActive.HasValue) IsActive = isActive.Value;

        UpdatedAt = DateTime.UtcNow;
        if (raiseUpdatedEvent)
            AddDomainEvent(new ParkingSpaceUpdatedEvent(Id, Title));
    }

    public void ToggleActive()
    {
        IsActive = !IsActive;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ParkingSpaceToggledEvent(Id, IsActive));
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ParkingSpaceToggledEvent(Id, IsActive));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ParkingSpaceToggledEvent(Id, IsActive));
    }

    public void MarkVerified()
    {
        IsVerified = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft-retire the lot (inactive + deleted flag) and raise deleted event for side effects.
    /// </summary>
    public void Retire(Guid actorUserId)
    {
        IsActive = false;
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ParkingSpaceDeletedEvent(Id, actorUserId));
    }

    public void SetImageUrlsCsv(string? imageUrlsCsv)
    {
        ImageUrls = string.IsNullOrWhiteSpace(imageUrlsCsv) ? null : imageUrlsCsv;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ParkingSpaceUpdatedEvent(Id, Title));
    }

    public void AppendImageUrls(IEnumerable<string> newUrls)
    {
        var urls = newUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList()
                   ?? new List<string>();
        if (urls.Count == 0)
            return;

        var existing = string.IsNullOrEmpty(ImageUrls)
            ? new List<string>()
            : ImageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        existing.AddRange(urls);
        ImageUrls = string.Join(",", existing.Distinct(StringComparer.OrdinalIgnoreCase));
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ParkingSpaceUpdatedEvent(Id, Title));
    }

    public void AssignOwnerNavigation(User owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    // ── Ratings ───────────────────────────────────────────────────

    public void RecordNewReview(int rating)
    {
        EnsureValidRating(rating);
        TotalReviews++;
        if (TotalReviews == 1)
            AverageRating = rating;
        else
            AverageRating = ((AverageRating * (TotalReviews - 1)) + rating) / TotalReviews;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReplaceReviewRating(int oldRating, int newRating)
    {
        EnsureValidRating(oldRating);
        EnsureValidRating(newRating);
        if (TotalReviews <= 0) return;
        var currentTotal = AverageRating * TotalReviews;
        AverageRating = (currentTotal - oldRating + newRating) / TotalReviews;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveReviewRating(int rating)
    {
        EnsureValidRating(rating);
        if (TotalReviews <= 0) return;
        if (TotalReviews == 1)
        {
            AverageRating = 0;
            TotalReviews = 0;
        }
        else
        {
            var currentTotal = AverageRating * TotalReviews;
            TotalReviews--;
            AverageRating = (currentTotal - rating) / TotalReviews;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Internals ─────────────────────────────────────────────────

    private static ParkingSpace CreateCore(
        Guid ownerId,
        string title,
        string description,
        string address,
        string city,
        string state,
        string country,
        string postalCode,
        double latitude,
        double longitude,
        ParkingType parkingType,
        int totalSpots,
        decimal hourlyRate,
        decimal dailyRate,
        decimal weeklyRate,
        decimal monthlyRate,
        TimeSpan? openTime,
        TimeSpan? closeTime,
        bool is24Hours,
        IEnumerable<string>? amenities,
        IEnumerable<string>? allowedVehicleTypes,
        IEnumerable<string>? imageUrls,
        string? specialInstructions,
        string? zoneCode)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("title", "Title is required");
        if (totalSpots < 1)
            throw new ValidationException("totalSpots", "Total spots must be at least 1");

        // Validate location as Address VO (still stored flattened for queries/geo)
        Address validatedAddress;
        try
        {
            validatedAddress = new Address(
                address ?? string.Empty,
                city ?? string.Empty,
                state ?? string.Empty,
                country ?? string.Empty,
                postalCode ?? string.Empty,
                latitude,
                longitude);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException("address", ex.Message);
        }

        var parking = new ParkingSpace
        {
            OwnerId = ownerId,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Address = validatedAddress.Street,
            City = validatedAddress.City,
            State = validatedAddress.State,
            Country = validatedAddress.Country,
            PostalCode = validatedAddress.PostalCode,
            ZoneCode = string.IsNullOrWhiteSpace(zoneCode) ? null : zoneCode.Trim().ToUpperInvariant(),
            Latitude = validatedAddress.Latitude,
            Longitude = validatedAddress.Longitude,
            ParkingType = parkingType,
            TotalSpots = totalSpots,
            AvailableSpots = totalSpots,
            HourlyRate = RequireNonNegative(hourlyRate, nameof(hourlyRate)),
            DailyRate = RequireNonNegative(dailyRate, nameof(dailyRate)),
            WeeklyRate = RequireNonNegative(weeklyRate, nameof(weeklyRate)),
            MonthlyRate = RequireNonNegative(monthlyRate, nameof(monthlyRate)),
            OpenTime = openTime ?? TimeSpan.Zero,
            CloseTime = closeTime ?? new TimeSpan(23, 59, 59),
            Is24Hours = is24Hours,
            Amenities = JoinCsv(amenities),
            AllowedVehicleTypes = JoinCsv(allowedVehicleTypes),
            ImageUrls = JoinCsv(imageUrls),
            SpecialInstructions = string.IsNullOrWhiteSpace(specialInstructions) ? null : specialInstructions.Trim(),
            IsActive = true
        };

        parking.SyncLocationFromCoordinates();
        return parking;
    }

    private void SyncLocationFromCoordinates()
    {
        Location = new Point(Longitude, Latitude) { SRID = 4326 };
    }

    private static decimal RequireNonNegative(decimal value, string name)
    {
        if (value < 0)
            throw new ValidationException(name, $"{name} cannot be negative");
        return value;
    }

    private static void EnsureValidRating(int rating)
    {
        if (rating is < 1 or > 5)
            throw new ValidationException("rating", "Rating must be between 1 and 5");
    }

    private static string? JoinCsv(IEnumerable<string>? values)
    {
        if (values == null) return null;
        var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();
        return list.Count == 0 ? null : string.Join(",", list);
    }
}

public class ParkingAvailability : BaseEntity
{
    public Guid ParkingSpaceId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int AvailableSpots { get; set; }

    public virtual ParkingSpace ParkingSpace { get; set; } = null!;
}
