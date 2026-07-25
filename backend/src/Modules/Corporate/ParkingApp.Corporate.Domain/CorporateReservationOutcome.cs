namespace ParkingApp.Corporate.Domain;

public sealed record CorporateReservationOutcome(
    CorporateBooking? Booking,
    CorporateWaitlistEntry? WaitlistEntry,
    CorporateFraudAssessment FraudAssessment,
    MarketplaceBookingAdjustment? MarketplaceAdjustment = null)
{
    public bool IsWaitlisted => WaitlistEntry != null;
}
