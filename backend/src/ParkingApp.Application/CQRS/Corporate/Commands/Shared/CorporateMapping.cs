using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Shared;

internal static class CorporateMapping
{
    public static CompanyDto ToCompanyDto(Company company) => new(
        company.Id,
        company.Name,
        company.RegistrationNumber,
        company.ContactEmail,
        company.ContactPhone,
        company.BillingAddress,
        company.BillingType,
        company.IsActive,
        company.Memberships.Count(m => !m.IsDeleted),
        company.Allocations.Count(a => a.Status == AllocationStatus.Active && !a.IsDeleted),
        company.CreatedAt);

    public static ParkingAllocationDto ToAllocationDto(
        ParkingAllocation allocation,
        string parkingSpaceTitle,
        string? vendorName = null) => new(
        allocation.Id,
        allocation.CompanyId,
        allocation.ParkingSpaceId,
        parkingSpaceTitle,
        allocation.Quota.TotalSlots,
        allocation.Quota.FixedSlots,
        allocation.Quota.SharedSlots,
        allocation.MonthlyRate,
        allocation.StartDate,
        allocation.EndDate,
        allocation.Status,
        allocation.SourceType,
        allocation.VendorId,
        allocation.LeaseReference,
        allocation.ApprovedByUserId,
        allocation.ApprovedAt,
        new BookingPolicyDto(
            allocation.BookingPolicy.MaxBookingsPerEmployeePerDay,
            allocation.BookingPolicy.MaxBookingsPerEmployeePerWeek,
            allocation.BookingPolicy.PriorityThreshold,
            allocation.BookingPolicy.AllowedStartTime,
            allocation.BookingPolicy.AllowedEndTime,
            allocation.BookingPolicy.AllowWeekends),
        allocation.FixedAssignments
            .Where(f => !f.IsDeleted)
            .Select(f => new FixedSlotAssignmentDto(f.MembershipId, string.Empty, f.SlotNumber, f.AssignedAt))
            .ToList(),
        allocation.CreatedAt,
        vendorName);

    public static CorporateParkingSpaceDto ToCorporateParkingSpaceDto(ParkingSpace parking, Guid companyId) => new(
        parking.Id,
        companyId,
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
        parking.SpecialInstructions,
        parking.ZoneCode,
        parking.CreatedAt);

    public static CorporateBookingDto ToCorporateBookingDto(CorporateBooking corporateBooking, Booking booking) => new(
        corporateBooking.Id,
        booking.Id,
        booking.BookingReference,
        corporateBooking.SlotType,
        booking.SlotNumber,
        corporateBooking.IsVisitorBooking,
        corporateBooking.VisitorName,
        corporateBooking.VisitorLicensePlate,
        booking.StartDateTime,
        booking.EndDateTime,
        booking.Status,
        corporateBooking.AccessPolicy?.QrCodeToken ?? booking.QRCode,
        corporateBooking.CreatedAt,
        corporateBooking.AllocationId,
        ParkingSpaceTitle: null,
        corporateBooking.MembershipId,
        MemberName: null,
        MemberEmail: null,
        booking.TotalAmount,
        booking.VehicleNumber);

    public static CorporateWaitlistDto ToWaitlistDto(CorporateWaitlistEntry waitlistEntry, int position) => new(
        waitlistEntry.Id,
        waitlistEntry.AllocationId,
        waitlistEntry.IsVisitorBooking,
        waitlistEntry.RequestedStartDateTime,
        waitlistEntry.RequestedEndDateTime,
        waitlistEntry.VehicleNumber,
        waitlistEntry.VisitorName,
        waitlistEntry.VisitorLicensePlate,
        waitlistEntry.Status,
        waitlistEntry.PriorityAtRequest,
        position,
        waitlistEntry.CreatedAt);

    public static FraudAssessmentDto ToFraudAssessmentDto(CorporateFraudAssessment fraudAssessment) => new(
        fraudAssessment.RiskLevel,
        fraudAssessment.IsBlocked,
        fraudAssessment.Reason);

    public static CorporateInvoiceLineDto ToInvoiceLineDto(CorporateInvoiceLineItem line) => new(
        line.Id,
        line.LineType,
        line.AllocationId,
        line.BookingId,
        line.Description,
        line.Quantity,
        line.UnitAmount,
        line.Amount);

    public static CorporateInvoiceDetailDto ToInvoiceDetailDto(CorporateInvoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.BillingTypeSnapshot,
        invoice.PeriodStart,
        invoice.PeriodEnd,
        invoice.Status,
        invoice.Currency,
        invoice.Subtotal,
        invoice.TaxAmount,
        invoice.TotalAmount,
        invoice.GeneratedByUserId,
        invoice.CreatedAt,
        invoice.IssuedAt,
        invoice.IssuedByUserId,
        invoice.PaidAt,
        invoice.PaidByUserId,
        invoice.PaymentReference,
        invoice.PaymentNotes,
        invoice.VoidedAt,
        invoice.VoidedByUserId,
        invoice.VoidReason,
        invoice.LineItems
            .Where(l => !l.IsDeleted)
            .OrderBy(l => l.Description)
            .Select(ToInvoiceLineDto)
            .ToList());

    public static CorporateInvoiceSummaryDto ToInvoiceSummaryDto(CorporateInvoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.BillingTypeSnapshot,
        invoice.PeriodStart,
        invoice.PeriodEnd,
        invoice.Status,
        invoice.Currency,
        invoice.Subtotal,
        invoice.TaxAmount,
        invoice.TotalAmount,
        invoice.LineItems.Count(l => !l.IsDeleted),
        invoice.CreatedAt,
        invoice.IssuedAt,
        invoice.PaidAt,
        invoice.PaymentReference);

    public static CorporateReservationResultDto ToReservationResultDto(CorporateReservationOutcome reservation, Booking? booking, Company company)
    {
        var bookingDto = reservation.Booking != null && booking != null
            ? ToCorporateBookingDto(reservation.Booking, booking)
            : null;

        var waitlistDto = reservation.WaitlistEntry != null
            ? ToWaitlistDto(reservation.WaitlistEntry, company.GetWaitlistPosition(reservation.WaitlistEntry.Id))
            : null;

        return new CorporateReservationResultDto(
            bookingDto,
            waitlistDto,
            ToFraudAssessmentDto(reservation.FraudAssessment));
    }

    private static List<string> ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();
    }

    private static List<VehicleType> ParseVehicleTypes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<VehicleType>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Enum.TryParse<VehicleType>(s.Trim(), out var vehicleType) ? vehicleType : VehicleType.Car)
            .ToList();
    }
}
