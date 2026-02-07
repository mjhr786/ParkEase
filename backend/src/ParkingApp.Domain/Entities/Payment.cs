using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    
    // Payment details
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    
    // Transaction info
    public string? TransactionId { get; set; }
    public string? PaymentGatewayReference { get; set; }
    public string? PaymentGateway { get; set; }
    
    // Timestamps
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    
    // Refund details
    public decimal? RefundAmount { get; set; }
    public string? RefundReason { get; set; }
    public string? RefundTransactionId { get; set; }
    
    // Receipt
    public string? ReceiptUrl { get; set; }
    public string? InvoiceNumber { get; set; }
    
    // Additional info
    public string? FailureReason { get; set; }
    public string? Metadata { get; set; } // JSON for additional payment info
    
    // Navigation properties
    public virtual Booking Booking { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
