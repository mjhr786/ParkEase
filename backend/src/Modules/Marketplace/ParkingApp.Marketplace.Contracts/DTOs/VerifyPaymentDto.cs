namespace ParkingApp.Marketplace.Contracts.DTOs;

public class VerifyPaymentDto
{
    public Guid BookingId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public string? RazorpayOrderId { get; set; }
    public string? RazorpaySignature { get; set; }

    /// <summary>Optional rail hint only (KD-PAY-22). Never skips matcher; contradicting hint → 400 rail_mismatch.</summary>
    public string? Rail { get; set; }
}

