using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Application.Mappings;

/// <summary>Marketplace module mappings.</summary>
public static class MarketplaceMappings
{
    public static ParkingSpaceDto ToDto(this ParkingSpace parking) => new(
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
        parking.IsCorporateOnly
    );

    public static ParkingSpaceDto ToDtoWithReservations(this ParkingSpace parking, IEnumerable<Booking> activeBookings)
    {
        var reservations = activeBookings
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
            null,
            null,
            reservations,
            parking.ZoneCode,
            parking.CompanyOwnerId,
            parking.OwnershipType,
            parking.IsCorporateOnly
        );
    }

    public static ParkingSpaceDto ToDtoWithFullDetails(
        this ParkingSpace parking,
        IEnumerable<Booking> activeBookings,
        double? distanceKm = null,
        int? durationMinutes = null)
    {
        var reservations = activeBookings
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
            parking.IsCorporateOnly
        );
    }

    public static ParkingSpace ToEntity(this CreateParkingSpaceDto dto, Guid ownerId) =>
        ParkingSpace.CreateForVendor(
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

    public static ParkingSpace ToCompanyEntity(this CreateParkingSpaceDto dto, Guid adminUserId, Guid companyId) =>
        ParkingSpace.CreateForCompany(
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
        booking.ParkingPassId.HasValue
    );

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

