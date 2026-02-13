using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.Mappings;

public static class MappingExtensions
{
    // User mappings
    public static UserDto ToDto(this User user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.PhoneNumber,
        user.Role,
        user.IsEmailVerified,
        user.IsPhoneVerified,
        user.CreatedAt
    );

    // ParkingSpace mappings
    public static ParkingSpaceDto ToDto(this ParkingSpace parking) => new(
        parking.Id,
        parking.OwnerId,
        parking.Owner?.FullName ?? "Unknown",
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
        null
    );

    public static ParkingSpaceDto ToDtoWithReservations(this ParkingSpace parking, IEnumerable<Booking> activeBookings)
    {
        var reservations = activeBookings
            .Where(b => b.Status == BookingStatus.Confirmed || 
                        b.Status == BookingStatus.AwaitingPayment || 
                        b.Status == BookingStatus.Pending ||
                        b.Status == BookingStatus.InProgress)
            .Where(b => b.EndDateTime > DateTime.UtcNow)
            .OrderBy(b => b.StartDateTime)
            .Select(b => new ReservationPeriodDto(b.StartDateTime, b.EndDateTime))
            .ToList();

        return new ParkingSpaceDto(
            parking.Id,
            parking.OwnerId,
            parking.Owner?.FullName ?? "Unknown",
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
            reservations
        );
    }

    public static ParkingSpace ToEntity(this CreateParkingSpaceDto dto, Guid ownerId) => new()
    {
        OwnerId = ownerId,
        Title = dto.Title,
        Description = dto.Description,
        Address = dto.Address,
        City = dto.City,
        State = dto.State,
        Country = dto.Country,
        PostalCode = dto.PostalCode,
        Latitude = dto.Latitude,
        Longitude = dto.Longitude,
        Location = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 },
        ParkingType = dto.ParkingType,
        TotalSpots = dto.TotalSpots,
        AvailableSpots = dto.TotalSpots,
        HourlyRate = dto.HourlyRate,
        DailyRate = dto.DailyRate,
        WeeklyRate = dto.WeeklyRate,
        MonthlyRate = dto.MonthlyRate,
        OpenTime = dto.OpenTime ?? new TimeSpan(0, 0, 0),
        CloseTime = dto.CloseTime ?? new TimeSpan(23, 59, 59),
        Is24Hours = dto.Is24Hours,
        Amenities = dto.Amenities != null ? string.Join(",", dto.Amenities) : null,
        AllowedVehicleTypes = dto.AllowedVehicleTypes != null 
            ? string.Join(",", dto.AllowedVehicleTypes.Select(v => v.ToString())) 
            : null,
        ImageUrls = dto.ImageUrls != null ? string.Join(",", dto.ImageUrls) : null,
        SpecialInstructions = dto.SpecialInstructions
    };

    // Booking mappings
    public static BookingDto ToDto(this Booking booking) => new(
        booking.Id,
        booking.UserId,
        booking.User?.FullName ?? "Unknown",
        booking.ParkingSpaceId,
        booking.ParkingSpace?.Title ?? "Unknown",
        booking.ParkingSpace?.Address ?? "Unknown",
        booking.StartDateTime,
        booking.EndDateTime,
        booking.PricingType,
        booking.VehicleType,
        booking.VehicleNumber,
        booking.VehicleModel,
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
        booking.CreatedAt
    );

    // Payment mappings
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

    // Review mappings
    public static ReviewDto ToDto(this Review review) => new(
        review.Id,
        review.UserId,
        review.User?.FullName ?? "Unknown",
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

    // Helper methods
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
