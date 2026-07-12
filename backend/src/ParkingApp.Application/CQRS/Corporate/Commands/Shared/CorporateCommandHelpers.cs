using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Shared;

internal static class CorporateCommandHelpers
{
    public static BookingPolicy? CreateBookingPolicy(BookingPolicyDto? dto)
    {
        if (dto == null)
        {
            return null;
        }

        return BookingPolicy.Create(
            dto.MaxBookingsPerEmployeePerDay,
            dto.MaxBookingsPerEmployeePerWeek,
            dto.PriorityThreshold,
            dto.AllowedStartTime ?? new TimeSpan(7, 0, 0),
            dto.AllowedEndTime ?? new TimeSpan(22, 0, 0),
            dto.AllowWeekends);
    }

    public static Booking CreateEmployeeBooking(BookCorporateParkingCommand command, Guid parkingSpaceId, decimal amount)
    {
        return Booking.CreateCorporateEmployee(
            command.UserId,
            parkingSpaceId,
            command.Dto.StartDateTime,
            command.Dto.EndDateTime,
            command.Dto.VehicleType,
            amount,
            command.Dto.VehicleNumber,
            GenerateBookingReference("CORP"));
    }

    public static Booking CreateVisitorBooking(BookVisitorParkingCommand command, Guid parkingSpaceId, decimal amount)
    {
        return Booking.CreateCorporateVisitor(
            command.UserId,
            parkingSpaceId,
            command.Dto.StartDateTime,
            command.Dto.EndDateTime,
            amount,
            command.Dto.VisitorLicensePlate,
            GenerateBookingReference("VIS"));
    }

    public static Booking CreateBookingFromWaitlist(CorporateWaitlistEntry waitlistEntry, Guid userId, Guid parkingSpaceId, decimal amount)
    {
        if (waitlistEntry.IsVisitorBooking)
        {
            return Booking.CreateCorporateVisitor(
                userId,
                parkingSpaceId,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                amount,
                waitlistEntry.VisitorLicensePlate,
                GenerateBookingReference("VIS"));
        }

        return Booking.CreateCorporateEmployee(
            userId,
            parkingSpaceId,
            waitlistEntry.RequestedStartDateTime,
            waitlistEntry.RequestedEndDateTime,
            waitlistEntry.VehicleType,
            amount,
            waitlistEntry.VehicleNumber,
            GenerateBookingReference("CORP"));
    }

    public static string GenerateBookingReference(string prefix)
    {
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    public static DateOnly GetWeekStart(DateOnly date)
    {
        var diff = (7 + ((int)date.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }

    public static string BuildLockKey(Guid companyId, Guid allocationId, DateTime startUtc)
    {
        return $"lock:corp-booking:{companyId}:{allocationId}:{startUtc:yyyyMMddHH}";
    }
}
