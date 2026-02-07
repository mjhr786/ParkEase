using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Entities;

public class Booking : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ParkingSpaceId { get; set; }
    
    // Booking period
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public PricingType PricingType { get; set; }
    
    // Vehicle info
    public VehicleType VehicleType { get; set; }
    public string? VehicleNumber { get; set; }
    public string? VehicleModel { get; set; }
    
    // Pricing
    public decimal BaseAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? DiscountCode { get; set; }
    
    // Status
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    
    // Confirmation
    public string? BookingReference { get; set; }
    public string? QRCode { get; set; }
    
    // Check-in/out
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    
    // Cancellation
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public decimal? RefundAmount { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ParkingSpace ParkingSpace { get; set; } = null!;
    public virtual Payment? Payment { get; set; }
    
    // Calculated properties
    public TimeSpan Duration => EndDateTime - StartDateTime;
    public bool IsActive => Status == BookingStatus.Confirmed || Status == BookingStatus.InProgress;
    
    // Domain Methods
    public void Confirm()
    {
        if (Status != BookingStatus.Pending && Status != BookingStatus.AwaitingPayment)
            throw new InvalidOperationException($"Cannot confirm booking in {Status} status");
        
        Status = BookingStatus.Confirmed;
    }
    
    public void AwaitPayment()
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Cannot set awaiting payment from {Status} status");
        
        Status = BookingStatus.AwaitingPayment;
    }
    
    public void Cancel(string reason)
    {
        if (Status == BookingStatus.Completed || Status == BookingStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel booking in {Status} status");
        
        Status = BookingStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
    }
    
    public void Reject(string reason)
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException($"Can only reject pending bookings");
        
        Status = BookingStatus.Rejected;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
    }
    
    public void CheckIn()
    {
        if (Status != BookingStatus.Confirmed)
            throw new InvalidOperationException($"Cannot check in booking in {Status} status");
        
        if (DateTime.UtcNow < StartDateTime.AddHours(-1))
            throw new InvalidOperationException("Check-in is only allowed within 1 hour before start time");
        
        Status = BookingStatus.InProgress;
        CheckInTime = DateTime.UtcNow;
    }
    
    public void CheckOut()
    {
        if (Status != BookingStatus.InProgress)
            throw new InvalidOperationException($"Cannot check out booking in {Status} status");
        
        Status = BookingStatus.Completed;
        CheckOutTime = DateTime.UtcNow;
    }
    
    public void ApplyDiscount(string discountCode, decimal discountAmount)
    {
        if (Status != BookingStatus.Pending)
            throw new InvalidOperationException("Can only apply discount to pending bookings");
        
        if (discountAmount < 0 || discountAmount > BaseAmount)
            throw new ArgumentException("Invalid discount amount");
        
        DiscountCode = discountCode;
        DiscountAmount = discountAmount;
        TotalAmount = BaseAmount + TaxAmount + ServiceFee - DiscountAmount;
    }
}
