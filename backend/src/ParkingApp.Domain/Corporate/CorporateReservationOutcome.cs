using ParkingApp.Domain.Marketplace;
namespace ParkingApp.Domain.Corporate;

public sealed record CorporateReservationOutcome(
    CorporateBooking? Booking,
    CorporateWaitlistEntry? WaitlistEntry,
    CorporateFraudAssessment FraudAssessment)
{
    public bool IsWaitlisted => WaitlistEntry != null;
}
