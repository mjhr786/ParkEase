using System;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Domain;
using System.Linq;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Shared;

internal static class CorporateMapping
{
    public static CompanyDto ToCompanyDto(Company company)
    {
        return null!; // Stubbed during modular monolith refactoring
    }

    public static CorporateInvoiceDetailDto ToInvoiceDetailDto(CorporateInvoice invoice)
    {
        return null!; // Stubbed during modular monolith refactoring
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
