namespace ParkingApp.Application.DTOs;

public class VerifyPaymentDto
{
    public Guid BookingId { get; set; }
    public string RazorpayPaymentId { get; set; }
    public string RazorpayOrderId { get; set; }
    public string RazorpaySignature { get; set; }
}
