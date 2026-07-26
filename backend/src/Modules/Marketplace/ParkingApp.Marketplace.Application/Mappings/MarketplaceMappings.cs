using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Services;

namespace ParkingApp.Marketplace.Application.Mappings;

/// <summary>Marketplace module mappings.</summary>
public static class MarketplaceMappings
{
    public static ParkingSpaceDto ToDto(this ParkingSpace parking, DateTime? priceAsOfUtc = null, int? availableSpotsForPrice = null)
    {
        var dyn = parking.GetEffectiveHourlyRate(priceAsOfUtc, availableSpotsForPrice);
        return new(
        parking.Id,
        parking.OwnerId,
        "Unknown", // Owner via IUserLookup when needed
            parking.Title,
        parking.Description,
        parking.Address,
        parking.City,
        parking.State,
        parking.Country,
        parking.PostalCode,
        parking.Latitude,
        parking.Longitude,
        parking.ParkingType,
        parking.TotalSpots,
        parking.AvailableSpots,
        parking.HourlyRate,
        parking.DailyRate,
        parking.WeeklyRate,
        parking.MonthlyRate,
        parking.OpenTime,
        parking.CloseTime,
        parking.Is24Hours,
        ParseCommaSeparated(parking.Amenities),
        ParseVehicleTypes(parking.AllowedVehicleTypes),
        ParseCommaSeparated(parking.ImageUrls),
        parking.IsActive,
        parking.IsVerified,
        parking.AverageRating,
        parking.TotalReviews,
        parking.SpecialInstructions,
        parking.CreatedAt,
        null,
        null,
        null,
        parking.ZoneCode,
        parking.CompanyOwnerId,
        parking.OwnershipType,
        parking.IsCorporateOnly,
        parking.IsLprEnabled,
        parking.IsDynamicPricingEnabled,
        parking.DynamicMinMultiplier,
        parking.DynamicMaxMultiplier,
        parking.PeakHourMultiplier,
        parking.WeekendMultiplier,
        parking.HasEvCharging,
        parking.EvChargerCount,
        parking.EvChargingRatePerHour,
        parking.EvIdleRatePerHour,
        parking.EvIdleGraceMinutes,
        parking.EvPricingMode,
        parking.EvRatePerKwh,
        parking.ListingCategory,
        parking.InstantBook,
        parking.TimeZoneId,
        dyn.EffectiveRate,
        dyn.Applied,
        dyn.Applied ? dyn.Multiplier : null,
        parking.IsBayGuidanceEnabled,
        parking.IsValetEnabled,
        parking.DefaultFacilityLevel,
        parking.DefaultFacilityZone,
        parking.IndoorGuidanceNotes
    );
    }

    public static ParkingSpaceDto ToDtoWithReservations(
        this ParkingSpace parking,
        IEnumerable<Booking> activeBookings,
        DateTime? priceAsOfUtc = null)
        => parking.ToDtoWithFullDetails(activeBookings, null, null, priceAsOfUtc);

    public static ParkingSpaceDto ToDtoWithFullDetails(
        this ParkingSpace parking,
        IEnumerable<Booking> activeBookings,
        double? distanceKm = null,
        int? durationMinutes = null,
        DateTime? priceAsOfUtc = null)
    {
        var asOf = priceAsOfUtc ?? DateTime.UtcNow;
        var bookingsList = activeBookings as IList<Booking> ?? activeBookings.ToList();
        var availableForPrice = EstimateAvailableAt(parking, bookingsList, asOf);
        var dyn = parking.GetEffectiveHourlyRate(asOf, availableForPrice);

        var reservations = bookingsList
            .Where(b => b.Status == BookingStatus.Confirmed ||
                        b.Status == BookingStatus.AwaitingPayment ||
                        b.Status == BookingStatus.Pending ||
                        b.Status == BookingStatus.InProgress ||
                        b.Status == BookingStatus.PendingExtension ||
                        b.Status == BookingStatus.AwaitingExtensionPayment)
            .Where(b => b.EndDateTime > DateTime.UtcNow)
            .OrderBy(b => b.StartDateTime)
            .Select(b => new ReservationPeriodDto(b.StartDateTime, b.EndDateTime, b.SlotNumber, null))
            .ToList();

        return new ParkingSpaceDto(
            parking.Id,
            parking.OwnerId,
            "Unknown", // Owner via IUserLookup when needed
            parking.Title,
            parking.Description,
            parking.Address,
            parking.City,
            parking.State,
            parking.Country,
            parking.PostalCode,
            parking.Latitude,
            parking.Longitude,
            parking.ParkingType,
            parking.TotalSpots,
            parking.AvailableSpots,
            parking.HourlyRate,
            parking.DailyRate,
            parking.WeeklyRate,
            parking.MonthlyRate,
            parking.OpenTime,
            parking.CloseTime,
            parking.Is24Hours,
            ParseCommaSeparated(parking.Amenities),
            ParseVehicleTypes(parking.AllowedVehicleTypes),
            ParseCommaSeparated(parking.ImageUrls),
            parking.IsActive,
            parking.IsVerified,
            parking.AverageRating,
            parking.TotalReviews,
            parking.SpecialInstructions,
            parking.CreatedAt,
            distanceKm,
            durationMinutes,
            reservations,
            parking.ZoneCode,
            parking.CompanyOwnerId,
            parking.OwnershipType,
            parking.IsCorporateOnly,
            parking.IsLprEnabled,
            parking.IsDynamicPricingEnabled,
            parking.DynamicMinMultiplier,
            parking.DynamicMaxMultiplier,
            parking.PeakHourMultiplier,
            parking.WeekendMultiplier,
            parking.HasEvCharging,
            parking.EvChargerCount,
            parking.EvChargingRatePerHour,
            parking.EvIdleRatePerHour,
            parking.EvIdleGraceMinutes,
            parking.EvPricingMode,
            parking.EvRatePerKwh,
            parking.ListingCategory,
            parking.InstantBook,
            parking.TimeZoneId,
            dyn.EffectiveRate,
            dyn.Applied,
            dyn.Applied ? dyn.Multiplier : null,
            parking.IsBayGuidanceEnabled,
            parking.IsValetEnabled,
            parking.DefaultFacilityLevel,
            parking.DefaultFacilityZone,
            parking.IndoorGuidanceNotes
        );
    }

    public static EvChargingSessionDto ToDto(this EvChargingSession session) => new(
        session.Id,
        session.BookingId,
        session.ParkingSpaceId,
        session.StationId,
        session.ConnectorId,
        session.OcppTransactionId,
        session.Status,
        session.StartedAtUtc,
        session.StoppedAtUtc,
        session.MeterStartKwh,
        session.LastMeterKwh,
        session.MeterEndKwh,
        session.EnergyDeliveredKwh,
        session.RatePerKwh,
        session.EnergyFeeAmount,
        session.Source
    );

    /// <summary>
    /// Estimate free spots at asOf from overlapping active bookings (for future start quotes).
    /// Falls back to entity AvailableSpots when no bookings provided.
    /// </summary>
    public static int EstimateAvailableAt(ParkingSpace parking, IEnumerable<Booking> bookings, DateTime asOfUtc)
    {
        var list = bookings as IList<Booking> ?? bookings.ToList();
        if (list.Count == 0)
            return parking.AvailableSpots;

        var blocking = list.Count(b =>
            b.Status is BookingStatus.Confirmed or BookingStatus.AwaitingPayment or BookingStatus.Pending
                or BookingStatus.InProgress or BookingStatus.PendingExtension or BookingStatus.AwaitingExtensionPayment
            && b.StartDateTime < asOfUtc.AddHours(1)
            && b.EndDateTime > asOfUtc);

        return Math.Clamp(parking.TotalSpots - blocking, 0, parking.TotalSpots);
    }

    public static ParkingSpace ToEntity(this CreateParkingSpaceDto dto, Guid ownerId)
    {
        var parking = ParkingSpace.CreateForVendor(
            ownerId,
            dto.Title,
            dto.Description,
            dto.Address,
            dto.City,
            dto.State,
            dto.Country,
            dto.PostalCode,
            dto.Latitude,
            dto.Longitude,
            dto.ParkingType,
            dto.TotalSpots,
            dto.HourlyRate,
            dto.DailyRate,
            dto.WeeklyRate,
            dto.MonthlyRate,
            dto.OpenTime,
            dto.CloseTime,
            dto.Is24Hours,
            dto.Amenities,
            dto.AllowedVehicleTypes?.Select(v => v.ToString()),
            dto.ImageUrls,
            dto.SpecialInstructions,
            dto.ZoneCode);
        if (dto.IsLprEnabled)
            parking.SetLprEnabled(true);
        if (dto.IsDynamicPricingEnabled
            || dto.DynamicMinMultiplier.HasValue
            || dto.DynamicMaxMultiplier.HasValue
            || dto.PeakHourMultiplier.HasValue
            || dto.WeekendMultiplier.HasValue
            || !string.IsNullOrWhiteSpace(dto.TimeZoneId))
        {
            parking.SetDynamicPricing(
                dto.IsDynamicPricingEnabled,
                dto.DynamicMinMultiplier,
                dto.DynamicMaxMultiplier,
                dto.PeakHourMultiplier,
                dto.WeekendMultiplier,
                dto.TimeZoneId);
        }
        else if (!string.IsNullOrWhiteSpace(dto.TimeZoneId))
        {
            parking.SetTimeZoneId(dto.TimeZoneId);
        }
        if (dto.HasEvCharging
            || dto.EvChargerCount.HasValue
            || dto.EvChargingRatePerHour.HasValue
            || dto.EvIdleRatePerHour.HasValue
            || dto.EvIdleGraceMinutes.HasValue
            || dto.EvPricingMode != EvPricingMode.Hourly
            || dto.EvRatePerKwh.HasValue)
        {
            parking.SetEvCharging(
                dto.HasEvCharging,
                dto.EvChargerCount,
                dto.EvChargingRatePerHour,
                dto.EvIdleRatePerHour,
                dto.EvIdleGraceMinutes,
                dto.EvPricingMode,
                dto.EvRatePerKwh);
        }
        if (dto.ListingCategory != ListingCategory.Commercial || dto.InstantBook == true)
        {
            parking.SetListingCategory(
                dto.ListingCategory,
                dto.InstantBook ?? (dto.ListingCategory == ListingCategory.Residential ? true : false));
        }
        if (dto.IsBayGuidanceEnabled
            || dto.IsValetEnabled
            || !string.IsNullOrWhiteSpace(dto.DefaultFacilityLevel)
            || !string.IsNullOrWhiteSpace(dto.DefaultFacilityZone)
            || !string.IsNullOrWhiteSpace(dto.IndoorGuidanceNotes))
        {
            parking.SetBayAndValet(
                dto.IsBayGuidanceEnabled,
                dto.IsValetEnabled,
                dto.DefaultFacilityLevel,
                dto.DefaultFacilityZone,
                dto.IndoorGuidanceNotes);
        }
        return parking;
    }

    public static ParkingSpace ToCompanyEntity(this CreateParkingSpaceDto dto, Guid adminUserId, Guid companyId)
    {
        var parking = ParkingSpace.CreateForCompany(
            adminUserId,
            companyId,
            dto.Title,
            dto.Description,
            dto.Address,
            dto.City,
            dto.State,
            dto.Country,
            dto.PostalCode,
            dto.Latitude,
            dto.Longitude,
            dto.ParkingType,
            dto.TotalSpots,
            dto.HourlyRate,
            dto.DailyRate,
            dto.WeeklyRate,
            dto.MonthlyRate,
            dto.OpenTime,
            dto.CloseTime,
            dto.Is24Hours,
            dto.Amenities,
            dto.AllowedVehicleTypes?.Select(v => v.ToString()),
            dto.ImageUrls,
            dto.SpecialInstructions,
            dto.ZoneCode);
        if (dto.IsLprEnabled)
            parking.SetLprEnabled(true);
        if (dto.IsDynamicPricingEnabled
            || dto.DynamicMinMultiplier.HasValue
            || dto.DynamicMaxMultiplier.HasValue
            || dto.PeakHourMultiplier.HasValue
            || dto.WeekendMultiplier.HasValue)
        {
            parking.SetDynamicPricing(
                dto.IsDynamicPricingEnabled,
                dto.DynamicMinMultiplier,
                dto.DynamicMaxMultiplier,
                dto.PeakHourMultiplier,
                dto.WeekendMultiplier);
        }
        if (dto.HasEvCharging
            || dto.EvChargerCount.HasValue
            || dto.EvChargingRatePerHour.HasValue
            || dto.EvIdleRatePerHour.HasValue
            || dto.EvIdleGraceMinutes.HasValue
            || dto.EvPricingMode != EvPricingMode.Hourly
            || dto.EvRatePerKwh.HasValue)
        {
            parking.SetEvCharging(
                dto.HasEvCharging,
                dto.EvChargerCount,
                dto.EvChargingRatePerHour,
                dto.EvIdleRatePerHour,
                dto.EvIdleGraceMinutes,
                dto.EvPricingMode,
                dto.EvRatePerKwh);
        }
        return parking;
    }

    public static BookingDto ToDto(this Booking booking) => new(
        booking.Id,
        booking.UserId,
        "Unknown", // User display name via IUserLookup when needed
        booking.ParkingSpaceId,
        booking.ParkingSpace?.Title ?? "Unknown",
        booking.ParkingSpace?.Address ?? "Unknown",
        booking.ParkingSpace?.Latitude ?? 0,
        booking.ParkingSpace?.Longitude ?? 0,
        booking.StartDateTime,
        booking.EndDateTime,
        booking.PricingType,
        booking.VehicleType,
        booking.SlotNumber,
        booking.VehicleNumber,
        booking.VehicleModel,
        booking.VehicleColor,
        booking.BaseAmount,
        booking.TaxAmount,
        booking.ServiceFee,
        booking.DiscountAmount,
        booking.TotalAmount,
        booking.DiscountCode,
        booking.Status,
        booking.BookingReference,
        booking.CheckInTime,
        booking.CheckOutTime,
        booking.Payment?.Status,
        booking.CreatedAt,
        booking.PendingExtensionEndDateTime,
        booking.PendingExtensionAmount,
        booking.HasPendingExtension,
        booking.ParkingPassId,
        booking.ParkingPass?.PassType.Kind.ToString(),
        booking.ParkingPassId.HasValue,
        booking.OverstayFeeAmount,
        booking.OverstayBillableMinutes,
        booking.OverstayFeeChargedAt,
        booking.OverstayFeePaidAmount,
        booking.OverstayFeeOutstanding,
        booking.QRCode,
        booking.IncludeEvCharging,
        booking.EvChargingFeeAmount,
        booking.EvIdleFeeAmount,
        booking.EventParkingPackageId,
        FacilityLevel: booking.FacilityLevel,
        FacilityZone: booking.FacilityZone,
        BayLabel: booking.BayLabel,
        ValetStatus: booking.ValetStatus,
        ValetRequestedAt: booking.ValetRequestedAt,
        ValetTargetReadyAt: booking.ValetTargetReadyAt,
        ValetReadyAt: booking.ValetReadyAt,
        ValetNotes: booking.ValetNotes,
        IsValetEnabled: booking.ParkingSpace?.IsValetEnabled ?? false,
        IsBayGuidanceEnabled: booking.ParkingSpace?.IsBayGuidanceEnabled ?? false,
        IndoorGuidanceNotes: booking.ParkingSpace?.IndoorGuidanceNotes,
        AncillarySubtotal: booking.AncillarySubtotal,
        AncillaryLines: booking.AncillaryLines.Select(ToAncillaryLineDto).ToList()
    );

    public static BookingDto ToDto(this Booking booking, EvChargingSession? latestEvSession) => new(
        booking.Id,
        booking.UserId,
        "Unknown",
        booking.ParkingSpaceId,
        booking.ParkingSpace?.Title ?? "Unknown",
        booking.ParkingSpace?.Address ?? "Unknown",
        booking.ParkingSpace?.Latitude ?? 0,
        booking.ParkingSpace?.Longitude ?? 0,
        booking.StartDateTime,
        booking.EndDateTime,
        booking.PricingType,
        booking.VehicleType,
        booking.SlotNumber,
        booking.VehicleNumber,
        booking.VehicleModel,
        booking.VehicleColor,
        booking.BaseAmount,
        booking.TaxAmount,
        booking.ServiceFee,
        booking.DiscountAmount,
        booking.TotalAmount,
        booking.DiscountCode,
        booking.Status,
        booking.BookingReference,
        booking.CheckInTime,
        booking.CheckOutTime,
        booking.Payment?.Status,
        booking.CreatedAt,
        booking.PendingExtensionEndDateTime,
        booking.PendingExtensionAmount,
        booking.HasPendingExtension,
        booking.ParkingPassId,
        booking.ParkingPass?.PassType.Kind.ToString(),
        booking.ParkingPassId.HasValue,
        booking.OverstayFeeAmount,
        booking.OverstayBillableMinutes,
        booking.OverstayFeeChargedAt,
        booking.OverstayFeePaidAmount,
        booking.OverstayFeeOutstanding,
        booking.QRCode,
        booking.IncludeEvCharging,
        booking.EvChargingFeeAmount,
        booking.EvIdleFeeAmount,
        booking.EventParkingPackageId,
        latestEvSession?.EnergyDeliveredKwh ?? 0m,
        latestEvSession?.Status.ToString(),
        latestEvSession?.OcppTransactionId,
        booking.ParkingSpace?.EvPricingMode,
        latestEvSession?.RatePerKwh ?? booking.ParkingSpace?.EvRatePerKwh,
        FacilityLevel: booking.FacilityLevel,
        FacilityZone: booking.FacilityZone,
        BayLabel: booking.BayLabel,
        ValetStatus: booking.ValetStatus,
        ValetRequestedAt: booking.ValetRequestedAt,
        ValetTargetReadyAt: booking.ValetTargetReadyAt,
        ValetReadyAt: booking.ValetReadyAt,
        ValetNotes: booking.ValetNotes,
        IsValetEnabled: booking.ParkingSpace?.IsValetEnabled ?? false,
        IsBayGuidanceEnabled: booking.ParkingSpace?.IsBayGuidanceEnabled ?? false,
        IndoorGuidanceNotes: booking.ParkingSpace?.IndoorGuidanceNotes,
        AncillarySubtotal: booking.AncillarySubtotal,
        AncillaryLines: booking.AncillaryLines.Select(ToAncillaryLineDto).ToList()
    );

    public static BookingAncillaryLineDto ToAncillaryLineDto(BookingAncillaryLine line) => new(
        line.Id,
        line.ServiceId,
        line.SnapshotName,
        line.UnitPrice,
        line.Quantity,
        line.LineTotal);

    public static ParkingPassDto ToDto(this ParkingPass parkingPass, DateTime? utcNow = null) => new(
        parkingPass.Id,
        parkingPass.UserId,
        "Unknown",
        parkingPass.PassType.Kind,
        parkingPass.Duration.StartDateUtc,
        parkingPass.Duration.EndDateUtc,
        parkingPass.CoverageType,
        parkingPass.ParkingSpaceId,
        parkingPass.ParkingSpace?.Title,
        parkingPass.ParkingZoneCode,
        parkingPass.UsagePolicy.Mode,
        parkingPass.UsagePolicy.DailyHourLimit,
        parkingPass.DiscountPercentage,
        parkingPass.GetState(utcNow ?? DateTime.UtcNow),
        parkingPass.IsActiveOn(utcNow ?? DateTime.UtcNow),
        parkingPass.IsExpiredOn(utcNow ?? DateTime.UtcNow),
        parkingPass.AllocatedByUserId,
        parkingPass.CorporateBatchReference,
        parkingPass.CreatedAt
    );

    public static PaymentDto ToDto(this Payment payment) => new(
        payment.Id,
        payment.BookingId,
        payment.UserId,
        payment.Amount,
        payment.Currency,
        payment.PaymentMethod,
        payment.Status,
        payment.TransactionId,
        payment.PaidAt,
        payment.ReceiptUrl,
        payment.InvoiceNumber,
        payment.CreatedAt
    );

    public static ReviewDto ToDto(this Review review) => new(
        review.Id,
        review.UserId,
        "Unknown", // Reviewer display name via IUserLookup when needed
        review.ParkingSpaceId,
        review.BookingId,
        review.Rating,
        review.Title,
        review.Comment,
        review.HelpfulCount,
        review.OwnerResponse,
        review.OwnerResponseAt,
        review.CreatedAt
    );

    private static List<string> ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<string>();
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();
    }

    private static List<VehicleType> ParseVehicleTypes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<VehicleType>();
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Enum.TryParse<VehicleType>(s.Trim(), out var vt) ? vt : VehicleType.Car)
                    .ToList();
    }
}

