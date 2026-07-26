using System;
using System.Linq;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Shared;

internal static class CorporateMapping
{
    public static CompanyDto ToCompanyDto(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);
        return new CompanyDto(
            company.Id,
            company.Name,
            company.RegistrationNumber,
            company.ContactEmail,
            company.ContactPhone,
            company.BillingAddress,
            company.BillingType,
            company.IsActive,
            company.Memberships.Count(m => !m.IsDeleted && m.IsActive),
            company.Allocations.Count(a => !a.IsDeleted && a.Status == AllocationStatus.Active),
            company.CreatedAt);
    }

    public static ParkingAllocationDto ToAllocationDto(
        ParkingAllocation allocation,
        string parkingSpaceTitle,
        string? vendorName = null)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        return new ParkingAllocationDto(
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
    }

    public static CorporateBookingDto ToCorporateBookingDto(
        CorporateBooking corporateBooking,
        BookingSnapshot booking)
    {
        Enum.TryParse<BookingStatus>(booking.Status, ignoreCase: true, out var status);
        return new CorporateBookingDto(
            corporateBooking.Id,
            booking.BookingId,
            booking.BookingReference,
            corporateBooking.SlotType,
            booking.SlotNumber,
            corporateBooking.IsVisitorBooking,
            corporateBooking.VisitorName,
            corporateBooking.VisitorLicensePlate,
            booking.StartUtc,
            booking.EndUtc,
            status,
            corporateBooking.AccessPolicy?.QrCodeToken ?? booking.QrCode,
            corporateBooking.CreatedAt,
            corporateBooking.AllocationId,
            ParkingSpaceTitle: null,
            corporateBooking.MembershipId,
            MemberName: null,
            MemberEmail: null,
            booking.TotalAmount,
            booking.VehicleNumber);
    }

    public static CorporateParkingSpaceDto ToCorporateParkingSpaceDto(CompanyOwnedParkingSpaceDetail space)
    {
        ArgumentNullException.ThrowIfNull(space);
        return new CorporateParkingSpaceDto(
            space.Id,
            space.CompanyId,
            space.Title,
            space.Description,
            space.Address,
            space.City,
            space.State,
            space.Country,
            space.PostalCode,
            space.Latitude,
            space.Longitude,
            space.ParkingType,
            space.TotalSpots,
            space.AvailableSpots,
            space.HourlyRate,
            space.DailyRate,
            space.WeeklyRate,
            space.MonthlyRate,
            space.OpenTime,
            space.CloseTime,
            space.Is24Hours,
            space.Amenities.ToList(),
            space.AllowedVehicleTypes.ToList(),
            space.ImageUrls.ToList(),
            space.IsActive,
            space.IsVerified,
            space.SpecialInstructions,
            space.ZoneCode,
            space.CreatedAt);
    }

    public static CorporateInvoiceDetailDto ToInvoiceDetailDto(CorporateInvoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var lines = invoice.LineItems
            .OrderBy(l => l.Description, StringComparer.OrdinalIgnoreCase)
            .Select(l => new CorporateInvoiceLineDto(
                l.Id,
                l.LineType,
                l.AllocationId,
                l.BookingId,
                l.Description,
                l.Quantity,
                l.UnitAmount,
                l.Amount))
            .ToList();

        return new CorporateInvoiceDetailDto(
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
            lines);
    }

    public static CorporateReservationResultDto ToReservationResultDto(CorporateReservationOutcome outcome, Company company, CorporateBookingDraft? draft = null)
    {
        CorporateBookingDto? bookingDto = null;
        if (outcome.Booking != null)
        {
            bookingDto = new CorporateBookingDto(
                Id: outcome.Booking.Id,
                BookingId: outcome.Booking.BookingId,
                BookingReference: null,
                SlotType: outcome.Booking.SlotType,
                SlotNumber: null,
                IsVisitorBooking: outcome.Booking.IsVisitorBooking,
                VisitorName: outcome.Booking.VisitorName,
                VisitorLicensePlate: outcome.Booking.VisitorLicensePlate,
                StartDateTime: draft?.StartUtc ?? default,
                EndDateTime: draft?.EndUtc ?? default,
                BookingStatus: draft?.Status ?? BookingStatus.Pending,
                QrCodeToken: null,
                CreatedAt: DateTime.UtcNow,
                AllocationId: outcome.Booking.AllocationId,
                ParkingSpaceTitle: null,
                MembershipId: outcome.Booking.MembershipId,
                MemberName: null,
                MemberEmail: null,
                TotalAmount: 0m,
                VehicleNumber: draft?.VehicleNumber
            );
        }

        CorporateWaitlistDto? waitlistDto = null;
        if (outcome.WaitlistEntry != null)
        {
            waitlistDto = new CorporateWaitlistDto(
                Id: outcome.WaitlistEntry.Id,
                AllocationId: outcome.WaitlistEntry.AllocationId,
                IsVisitorBooking: outcome.WaitlistEntry.IsVisitorBooking,
                RequestedStartDateTime: outcome.WaitlistEntry.RequestedStartDateTime,
                RequestedEndDateTime: outcome.WaitlistEntry.RequestedEndDateTime,
                VehicleNumber: outcome.WaitlistEntry.VehicleNumber,
                VisitorName: outcome.WaitlistEntry.VisitorName,
                VisitorLicensePlate: outcome.WaitlistEntry.VisitorLicensePlate,
                Status: outcome.WaitlistEntry.Status,
                PriorityAtRequest: outcome.WaitlistEntry.PriorityAtRequest,
                Position: company.GetWaitlistPosition(outcome.WaitlistEntry.Id),
                CreatedAt: DateTime.UtcNow
            );
        }

        var fraudDto = new FraudAssessmentDto(
            RiskLevel: outcome.FraudAssessment.RiskLevel,
            IsBlocked: outcome.FraudAssessment.IsBlocked,
            Reason: outcome.FraudAssessment.Reason
        );

        return new CorporateReservationResultDto(bookingDto, waitlistDto, fraudDto);
    }
}
