using ParkingApp.BuildingBlocks.Domain;
using NetTopologySuite.Geometries;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Marketplace.Domain.Services;
using ParkingApp.Marketplace.Domain.ValueObjects;
using ParkingApp.BuildingBlocks.ValueObjects;

namespace ParkingApp.Marketplace.Domain.Entities;

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

    /// <summary>
    /// When true, LPR gate access is accepted and bookings must include a license plate.
    /// </summary>
    public bool IsLprEnabled { get; internal set; }

    /// <summary>
    /// When true, booking base rates use demand-based multipliers (occupancy, peak hours, weekend).
    /// Stored base rates (Hourly/Daily/…) remain the list price; effective rate is computed at quote time.
    /// </summary>
    public bool IsDynamicPricingEnabled { get; internal set; }

    /// <summary>Floor for combined dynamic multiplier (default 0.80).</summary>
    public decimal DynamicMinMultiplier { get; internal set; } = 0.80m;

    /// <summary>Ceiling for combined dynamic multiplier (default 1.75).</summary>
    public decimal DynamicMaxMultiplier { get; internal set; } = 1.75m;

    /// <summary>Extra factor during peak windows when dynamic pricing is on (default 1.25).</summary>
    public decimal PeakHourMultiplier { get; internal set; } = 1.25m;

    /// <summary>Extra factor on Saturday/Sunday when dynamic pricing is on (default 1.15).</summary>
    public decimal WeekendMultiplier { get; internal set; } = 1.15m;

    /// <summary>
    /// IANA time zone id for peak/weekend evaluation (e.g. Asia/Kolkata). Empty/invalid → UTC.
    /// </summary>
    public string TimeZoneId { get; internal set; } = "UTC";

    /// <summary>When true, facility offers EV charging bays (searchable; booking can add charging fee).</summary>
    public bool HasEvCharging { get; internal set; }

    /// <summary>Number of EV charger bays (informational; capacity still uses TotalSpots).</summary>
    public int EvChargerCount { get; internal set; }

    /// <summary>Hourly EV charging surcharge applied when a booking includes charging (Hourly mode).</summary>
    public decimal EvChargingRatePerHour { get; internal set; }

    /// <summary>Hourly idle / charger-hogging fee after booking end + grace (EV bookings).</summary>
    public decimal EvIdleRatePerHour { get; internal set; }

    /// <summary>Minutes after EndDateTime before EV idle fees start (default 15).</summary>
    public int EvIdleGraceMinutes { get; internal set; } = 15;

    /// <summary>Hourly (Phase 1) vs per-kWh (Phase 2) energy pricing.</summary>
    public EvPricingMode EvPricingMode { get; internal set; } = EvPricingMode.Hourly;

    /// <summary>Energy rate (₹/kWh) when <see cref="EvPricingMode"/> is PerKwh.</summary>
    public decimal EvRatePerKwh { get; internal set; }

    /// <summary>Commercial garage/lot vs residential driveway / private home spot.</summary>
    public ListingCategory ListingCategory { get; internal set; } = ListingCategory.Commercial;

    /// <summary>
    /// When true, new marketplace bookings skip host approval and go to payment (or confirm if free).
    /// Residential listings default this on.
    /// </summary>
    public bool InstantBook { get; internal set; }

    /// <summary>When true, bookings receive assigned bay / level / zone guidance (no beacons required).</summary>
    public bool IsBayGuidanceEnabled { get; internal set; }

    /// <summary>When true, guests may request valet vehicle retrieval.</summary>
    public bool IsValetEnabled { get; internal set; }

    /// <summary>Default facility level label applied on bay auto-assign (e.g. P2).</summary>
    public string? DefaultFacilityLevel { get; internal set; }

    /// <summary>Default facility zone label applied on bay auto-assign (e.g. Blue).</summary>
    public string? DefaultFacilityZone { get; internal set; }

    /// <summary>Static indoor wayfinding notes shown to guests (mock guidance without beacons).</summary>
    public string? IndoorGuidanceNotes { get; internal set; }

    public double AverageRating { get; internal set; }
    public int TotalReviews { get; internal set; }

    public string? SpecialInstructions { get; internal set; }
    public virtual ICollection<Booking> Bookings { get; internal set; } = new List<Booking>();
    public virtual ICollection<Review> Reviews { get; internal set; } = new List<Review>();
    public virtual ICollection<ParkingAvailability> Availabilities { get; internal set; } = new List<ParkingAvailability>();
    public virtual ICollection<Favorite> FavoritedBy { get; internal set; } = new List<Favorite>();
    public virtual ICollection<ParkingPass> ParkingPasses { get; internal set; } = new List<ParkingPass>();

    internal ParkingSpace()
    {
    }

    // G��G�� Factories G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

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

    // G��G�� Updates G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

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
        bool? isLprEnabled = null,
        bool? isDynamicPricingEnabled = null,
        decimal? dynamicMinMultiplier = null,
        decimal? dynamicMaxMultiplier = null,
        decimal? peakHourMultiplier = null,
        decimal? weekendMultiplier = null,
        bool? hasEvCharging = null,
        int? evChargerCount = null,
        decimal? evChargingRatePerHour = null,
        decimal? evIdleRatePerHour = null,
        int? evIdleGraceMinutes = null,
        EvPricingMode? evPricingMode = null,
        decimal? evRatePerKwh = null,
        ListingCategory? listingCategory = null,
        bool? instantBook = null,
        string? timeZoneId = null,
        bool? isBayGuidanceEnabled = null,
        bool? isValetEnabled = null,
        string? defaultFacilityLevel = null,
        string? defaultFacilityZone = null,
        string? indoorGuidanceNotes = null,
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
            var maxSpots = (listingCategory ?? ListingCategory) == ListingCategory.Residential ? 10 : 1000;
            if (totalSpots.Value > maxSpots)
                throw new ValidationException("totalSpots", $"Total spots cannot exceed {maxSpots} for this listing type");
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
        if (isLprEnabled.HasValue) IsLprEnabled = isLprEnabled.Value;

        if (isDynamicPricingEnabled.HasValue
            || dynamicMinMultiplier.HasValue
            || dynamicMaxMultiplier.HasValue
            || peakHourMultiplier.HasValue
            || weekendMultiplier.HasValue
            || timeZoneId != null)
        {
            ApplyDynamicPricingSettings(
                isDynamicPricingEnabled ?? IsDynamicPricingEnabled,
                dynamicMinMultiplier ?? DynamicMinMultiplier,
                dynamicMaxMultiplier ?? DynamicMaxMultiplier,
                peakHourMultiplier ?? PeakHourMultiplier,
                weekendMultiplier ?? WeekendMultiplier,
                timeZoneId,
                raiseEvent: false);
        }
        else if (timeZoneId != null)
        {
            TimeZoneId = NormalizeTimeZoneId(timeZoneId);
        }

        if (hasEvCharging.HasValue
            || evChargerCount.HasValue
            || evChargingRatePerHour.HasValue
            || evIdleRatePerHour.HasValue
            || evIdleGraceMinutes.HasValue
            || evPricingMode.HasValue
            || evRatePerKwh.HasValue)
        {
            ApplyEvChargingSettings(
                hasEvCharging ?? HasEvCharging,
                evChargerCount ?? EvChargerCount,
                evChargingRatePerHour ?? EvChargingRatePerHour,
                evIdleRatePerHour ?? EvIdleRatePerHour,
                evIdleGraceMinutes ?? EvIdleGraceMinutes,
                evPricingMode ?? EvPricingMode,
                evRatePerKwh ?? EvRatePerKwh,
                raiseEvent: false);
        }

        if (listingCategory.HasValue)
            ApplyListingCategory(listingCategory.Value, instantBook, raiseEvent: false);
        else if (instantBook.HasValue)
            InstantBook = instantBook.Value;

        if (isBayGuidanceEnabled.HasValue
            || isValetEnabled.HasValue
            || defaultFacilityLevel != null
            || defaultFacilityZone != null
            || indoorGuidanceNotes != null)
        {
            ApplyBayAndValetSettings(
                isBayGuidanceEnabled ?? IsBayGuidanceEnabled,
                isValetEnabled ?? IsValetEnabled,
                defaultFacilityLevel ?? DefaultFacilityLevel,
                defaultFacilityZone ?? DefaultFacilityZone,
                indoorGuidanceNotes ?? IndoorGuidanceNotes,
                raiseEvent: false);
        }

        UpdatedAt = DateTime.UtcNow;
        if (raiseUpdatedEvent)
            AddDomainEvent(new ParkingSpaceUpdatedEvent(Id, Title));
    }

    /// <summary>
    /// Sets commercial vs residential listing. Residential caps spots and can default instant book.
    /// </summary>
    public void SetListingCategory(ListingCategory category, bool? instantBook = null)
    {
        ApplyListingCategory(category, instantBook, raiseEvent: true);
    }

    private void ApplyListingCategory(ListingCategory category, bool? instantBook, bool raiseEvent)
    {
        const int maxResidentialSpots = 10;

        ListingCategory = category;

        if (category == ListingCategory.Residential)
        {
            if (TotalSpots > maxResidentialSpots)
            {
                TotalSpots = maxResidentialSpots;
                AvailableSpots = Math.Min(AvailableSpots, TotalSpots);
            }

            InstantBook = instantBook ?? true;
            SyncDrivewayAmenity(enabled: true);
        }
        else
        {
            InstantBook = instantBook ?? InstantBook;
            SyncDrivewayAmenity(enabled: false);
        }

        UpdatedAt = DateTime.UtcNow;
        if (raiseEvent)
            AddDomainEvent(new ParkingSpaceUpdatedEvent(Id, Title));
    }

    private void SyncDrivewayAmenity(bool enabled)
    {
        var tags = ParseCsv(Amenities);
        const string driveway = "Driveway";
        var has = tags.Any(t => t.Equals(driveway, StringComparison.OrdinalIgnoreCase));
        if (enabled && !has)
            tags.Add(driveway);
        else if (!enabled && has)
            tags.RemoveAll(t => t.Equals(driveway, StringComparison.OrdinalIgnoreCase));
        Amenities = tags.Count == 0 ? null : string.Join(",", tags);
    }

    private static List<string> ParseCsv(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

    /// <summary>Enable or disable LPR gate access for this facility.</summary>
    public void SetLprEnabled(bool enabled)
    {
        if (IsLprEnabled == enabled) return;
        IsLprEnabled = enabled;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ParkingSpaceUpdatedEvent(Id, Title));
    }

    /// <summary>Enable/disable dynamic pricing and set multiplier bounds/factors.</summary>
    public void SetDynamicPricing(
        bool enabled,
        decimal? minMultiplier = null,
        decimal? maxMultiplier = null,
        decimal? peakHourMultiplier = null,
        decimal? weekendMultiplier = null,
        string? timeZoneId = null)
    {
        ApplyDynamicPricingSettings(
            enabled,
            minMultiplier ?? DynamicMinMultiplier,
            maxMultiplier ?? DynamicMaxMultiplier,
            peakHourMultiplier ?? PeakHourMultiplier,
            weekendMultiplier ?? WeekendMultiplier,
            timeZoneId,
            raiseEvent: true);
    }

    /// <summary>
    /// Effective hourly list rate after dynamic factors (for search “from ₹…” and quotes).
    /// </summary>
    public DynamicPricingResult GetEffectiveHourlyRate(
        DateTime? asOfUtc = null,
        int? availableSpotsOverride = null)
    {
        return DynamicPricingCalculator.Calculate(
            HourlyRate,
            IsDynamicPricingEnabled,
            TotalSpots,
            availableSpotsOverride ?? AvailableSpots,
            asOfUtc ?? DateTime.UtcNow,
            DynamicMinMultiplier,
            DynamicMaxMultiplier,
            PeakHourMultiplier,
            WeekendMultiplier,
            timeZoneId: TimeZoneId);
    }

    public void SetTimeZoneId(string? timeZoneId)
    {
        TimeZoneId = NormalizeTimeZoneId(timeZoneId);
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ParkingSpaceUpdatedEvent(Id, Title));
    }

    private void ApplyDynamicPricingSettings(
        bool enabled,
        decimal minMultiplier,
        decimal maxMultiplier,
        decimal peakHourMultiplier,
        decimal weekendMultiplier,
        string? timeZoneId,
        bool raiseEvent)
    {
        minMultiplier = ClampDynamicMultiplier(minMultiplier, 0.10m, 1.0m, nameof(DynamicMinMultiplier));
        maxMultiplier = ClampDynamicMultiplier(maxMultiplier, 1.0m, 5.0m, nameof(DynamicMaxMultiplier));
        if (maxMultiplier < minMultiplier)
            throw new ValidationException("dynamicMaxMultiplier", "Max multiplier must be >= min multiplier");

        peakHourMultiplier = ClampDynamicMultiplier(peakHourMultiplier, 1.0m, 3.0m, nameof(PeakHourMultiplier));
        weekendMultiplier = ClampDynamicMultiplier(weekendMultiplier, 1.0m, 3.0m, nameof(WeekendMultiplier));

        IsDynamicPricingEnabled = enabled;
        DynamicMinMultiplier = minMultiplier;
        DynamicMaxMultiplier = maxMultiplier;
        PeakHourMultiplier = peakHourMultiplier;
        if (timeZoneId != null)
            TimeZoneId = NormalizeTimeZoneId(timeZoneId);
        WeekendMultiplier = weekendMultiplier;
        UpdatedAt = DateTime.UtcNow;
        if (raiseEvent)
            AddDomainEvent(new ParkingSpaceUpdatedEvent(Id, Title));
    }

    private static decimal ClampDynamicMultiplier(decimal value, decimal min, decimal max, string field)
    {
        if (value < min || value > max)
            throw new ValidationException(field, $"{field} must be between {min} and {max}");
        return Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeTimeZoneId(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return "UTC";
        var id = timeZoneId.Trim();
        // Validate early so vendors get a clear error (calculator still falls back to UTC if unknown later).
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(id);
            return id;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ValidationException("timeZoneId", $"Unknown time zone '{id}'. Use an IANA id such as Asia/Kolkata or UTC.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new ValidationException("timeZoneId", $"Invalid time zone '{id}'.");
        }
    }

    /// <summary>Enable/configure EV charging inventory and rates for this facility.</summary>
    public void SetEvCharging(
        bool enabled,
        int? chargerCount = null,
        decimal? chargingRatePerHour = null,
        decimal? idleRatePerHour = null,
        int? idleGraceMinutes = null,
        EvPricingMode? pricingMode = null,
        decimal? ratePerKwh = null)
    {
        ApplyEvChargingSettings(
            enabled,
            chargerCount ?? EvChargerCount,
            chargingRatePerHour ?? EvChargingRatePerHour,
            idleRatePerHour ?? EvIdleRatePerHour,
            idleGraceMinutes ?? EvIdleGraceMinutes,
            pricingMode ?? EvPricingMode,
            ratePerKwh ?? EvRatePerKwh,
            raiseEvent: true);
    }

    /// <summary>Enable/configure indoor bay guidance and valet retrieval for this facility.</summary>
    public void SetBayAndValet(
        bool bayGuidanceEnabled,
        bool valetEnabled,
        string? defaultFacilityLevel = null,
        string? defaultFacilityZone = null,
        string? indoorGuidanceNotes = null)
    {
        ApplyBayAndValetSettings(
            bayGuidanceEnabled,
            valetEnabled,
            defaultFacilityLevel,
            defaultFacilityZone,
            indoorGuidanceNotes,
            raiseEvent: true);
    }

    private void ApplyBayAndValetSettings(
        bool bayGuidanceEnabled,
        bool valetEnabled,
        string? defaultFacilityLevel,
        string? defaultFacilityZone,
        string? indoorGuidanceNotes,
        bool raiseEvent)
    {
        IsBayGuidanceEnabled = bayGuidanceEnabled;
        IsValetEnabled = valetEnabled;
        DefaultFacilityLevel = NormalizeOptionalLabel(defaultFacilityLevel, 32);
        DefaultFacilityZone = NormalizeOptionalLabel(defaultFacilityZone, 64);
        IndoorGuidanceNotes = string.IsNullOrWhiteSpace(indoorGuidanceNotes)
            ? null
            : indoorGuidanceNotes.Trim();
        if (IndoorGuidanceNotes is { Length: > 2000 })
            throw new ValidationException("indoorGuidanceNotes", "Indoor guidance notes cannot exceed 2000 characters");

        SyncAmenityTag("Bay Guidance", bayGuidanceEnabled);
        SyncAmenityTag("Valet", valetEnabled);

        UpdatedAt = DateTime.UtcNow;
        if (raiseEvent)
            AddDomainEvent(new ParkingSpaceUpdatedEvent(Id, Title));
    }

    private void SyncAmenityTag(string tag, bool enabled)
    {
        var list = string.IsNullOrWhiteSpace(Amenities)
            ? new List<string>()
            : Amenities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();

        list.RemoveAll(a => a.Equals(tag, StringComparison.OrdinalIgnoreCase));
        if (enabled)
            list.Add(tag);

        Amenities = list.Count == 0 ? null : string.Join(",", list);
    }

    private static string? NormalizeOptionalLabel(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLen)
            throw new ValidationException("label", $"Label cannot exceed {maxLen} characters");
        return trimmed;
    }

    private void ApplyEvChargingSettings(
        bool enabled,
        int chargerCount,
        decimal chargingRatePerHour,
        decimal idleRatePerHour,
        int idleGraceMinutes,
        EvPricingMode pricingMode,
        decimal ratePerKwh,
        bool raiseEvent)
    {
        if (chargerCount < 0)
            throw new ValidationException("evChargerCount", "EV charger count cannot be negative");
        if (chargingRatePerHour < 0)
            throw new ValidationException("evChargingRatePerHour", "EV charging rate cannot be negative");
        if (idleRatePerHour < 0)
            throw new ValidationException("evIdleRatePerHour", "EV idle rate cannot be negative");
        if (ratePerKwh < 0)
            throw new ValidationException("evRatePerKwh", "EV rate per kWh cannot be negative");
        if (!Enum.IsDefined(pricingMode))
            throw new ValidationException("evPricingMode", "Invalid EV pricing mode");
        idleGraceMinutes = Math.Clamp(idleGraceMinutes, 0, 24 * 60);

        HasEvCharging = enabled;
        EvChargerCount = enabled ? Math.Max(chargerCount, 0) : 0;
        EvChargingRatePerHour = Math.Round(chargingRatePerHour, 2, MidpointRounding.AwayFromZero);
        EvIdleRatePerHour = Math.Round(idleRatePerHour, 2, MidpointRounding.AwayFromZero);
        EvIdleGraceMinutes = idleGraceMinutes;
        EvPricingMode = enabled ? pricingMode : EvPricingMode.Hourly;
        EvRatePerKwh = enabled
            ? Math.Round(ratePerKwh, 2, MidpointRounding.AwayFromZero)
            : 0m;

        SyncEvChargingAmenityTag(enabled);

        UpdatedAt = DateTime.UtcNow;
        if (raiseEvent)
            AddDomainEvent(new ParkingSpaceUpdatedEvent(Id, Title));
    }

    /// <summary>Keeps free-text Amenities searchable for legacy EV_Charging filters.</summary>
    private void SyncEvChargingAmenityTag(bool enabled)
    {
        const string tag = "EV Charging";
        const string tagAlt = "EV_Charging";
        var list = string.IsNullOrWhiteSpace(Amenities)
            ? new List<string>()
            : Amenities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();

        list.RemoveAll(a =>
            a.Equals(tag, StringComparison.OrdinalIgnoreCase)
            || a.Equals(tagAlt, StringComparison.OrdinalIgnoreCase)
            || a.Equals("EVCharging", StringComparison.OrdinalIgnoreCase));

        if (enabled)
            list.Add(tag);

        Amenities = list.Count == 0 ? null : string.Join(",", list);
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

    // G��G�� Ratings G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

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

    // G��G�� Internals G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

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

