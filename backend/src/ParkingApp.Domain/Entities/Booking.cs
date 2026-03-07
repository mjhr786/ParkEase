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
    public int? SlotNumber { get; set; }
    public string? VehicleNumber { get; set; }
    public string? VehicleModel { get; set; }
    public string? VehicleColor { get; set; }
    
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

    // Extension request (pending vendor approval)
    public DateTime? PendingExtensionEndDateTime { get; set; }
    public decimal? PendingExtensionAmount { get; set; }
    public bool HasPendingExtension => PendingExtensionEndDateTime.HasValue;

    // Status before extension was requested (to revert on rejection)
    public BookingStatus? PreExtensionStatus { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ParkingSpace ParkingSpace { get; set; } = null!;
    public virtual Payment? Payment { get; set; }
    
    // Calculated properties
    public TimeSpan Duration => EndDateTime - StartDateTime;
    public bool IsActive => Status == BookingStatus.Confirmed || Status == BookingStatus.InProgress;
    
    // ── Regular booking domain methods ────────────────────────────

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
            throw new InvalidOperationException("Can only reject pending bookings");
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

    // ── Extension domain methods ───────────────────────────────────

    /// <summary>
    /// User requests an extension. Stores the proposed new end time and extra cost,
    /// and moves the booking to PendingExtension status.
    /// </summary>
    public void RequestExtension(DateTime newEndDateTime, decimal extraAmount)
    {
        if (Status != BookingStatus.Confirmed && Status != BookingStatus.InProgress)
            throw new InvalidOperationException("Only confirmed or in-progress bookings can be extended");
        if (newEndDateTime <= EndDateTime)
            throw new InvalidOperationException("New end time must be after the current end time");
        if (extraAmount < 0)
            throw new ArgumentException("Extra amount cannot be negative");

        PreExtensionStatus = Status;
        PendingExtensionEndDateTime = newEndDateTime;
        PendingExtensionAmount = extraAmount;
        Status = BookingStatus.PendingExtension;
    }

    /// <summary>
    /// Vendor approves the extension request — moves to AwaitingExtensionPayment.
    /// </summary>
    public void ApproveExtension()
    {
        if (Status != BookingStatus.PendingExtension)
            throw new InvalidOperationException("Only pending extension requests can be approved");
        Status = BookingStatus.AwaitingExtensionPayment;
    }

    /// <summary>
    /// Vendor rejects the extension request — reverts to the original status.
    /// </summary>
    public void RejectExtension(string reason)
    {
        if (Status != BookingStatus.PendingExtension)
            throw new InvalidOperationException("Only pending extension requests can be rejected");
        Status = PreExtensionStatus ?? BookingStatus.Confirmed;
        CancellationReason = null; // keep cancellation clean
        PendingExtensionEndDateTime = null;
        PendingExtensionAmount = null;
        PreExtensionStatus = null;
    }

    /// <summary>
    /// Called after successful extension payment — updates EndDateTime and amounts,
    /// then reverts to Confirmed or InProgress.
    /// </summary>
    public void ConfirmExtension()
    {
        if (Status != BookingStatus.AwaitingExtensionPayment)
            throw new InvalidOperationException("Extension must be awaiting payment to confirm");
        if (!PendingExtensionEndDateTime.HasValue || !PendingExtensionAmount.HasValue)
            throw new InvalidOperationException("No pending extension to confirm");

        EndDateTime = PendingExtensionEndDateTime.Value;
        TotalAmount += PendingExtensionAmount.Value;
        // Restore the status that existed before the extension was requested
        Status = PreExtensionStatus ?? BookingStatus.Confirmed;
        PendingExtensionEndDateTime = null;
        PendingExtensionAmount = null;
        PreExtensionStatus = null;
    }
}
