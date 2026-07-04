namespace ParkingApp.Domain.Entities.Corporate;

public sealed record CorporateReservationOutcome(
    CorporateBooking? Booking,
    CorporateWaitlistEntry? WaitlistEntry,
    CorporateFraudAssessment FraudAssessment)
{
    public bool IsWaitlisted => WaitlistEntry != null;
}
