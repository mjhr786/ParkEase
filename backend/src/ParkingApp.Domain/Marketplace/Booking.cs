using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Shared;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Events.Bookings;

namespace ParkingApp.Domain.Marketplace;

/// <summary>
/// Booking aggregate root for marketplace and corporate parking reservations.
/// Create via factories; mutate only through domain methods.
/// </summary>
public class Booking : BaseEntity
{
    // internal set: Application cannot mutate; unit tests (InternalsVisibleTo) and domain methods can.
    public Guid UserId { get; internal set; }
    public Guid ParkingSpaceId { get; internal set; }
    public Guid? ParkingPassId { get; internal set; }

    public DateTime StartDateTime { get; internal set; }
    public DateTime EndDateTime { get; internal set; }
    public PricingType PricingType { get; internal set; }

    public VehicleType VehicleType { get; internal set; }
    public int? SlotNumber { get; internal set; }
    public string? VehicleNumber { get; internal set; }
    public string? VehicleModel { get; internal set; }
    public string? VehicleColor { get; internal set; }

    public decimal BaseAmount { get; internal set; }
    public decimal TaxAmount { get; internal set; }
    public decimal ServiceFee { get; internal set; }
    public decimal DiscountAmount { get; internal set; }
    public decimal TotalAmount { get; internal set; }
    public string? DiscountCode { get; internal set; }

    public BookingStatus Status { get; internal set; } = BookingStatus.Pending;

    public string? BookingReference { get; internal set; }
    public string? QRCode { get; internal set; }

    public DateTime? CheckInTime { get; internal set; }
    public DateTime? CheckOutTime { get; internal set; }

    public string? CancellationReason { get; internal set; }
    public DateTime? CancelledAt { get; internal set; }
    public decimal? RefundAmount { get; internal set; }

    public DateTime? PendingExtensionEndDateTime { get; internal set; }
    public decimal? PendingExtensionAmount { get; internal set; }
    public bool HasPendingExtension => PendingExtensionEndDateTime.HasValue;

    public BookingStatus? PreExtensionStatus { get; internal set; }

    public virtual User User { get; internal set; } = null!;
    public virtual ParkingSpace ParkingSpace { get; internal set; } = null!;
    public virtual ParkingPass? ParkingPass { get; internal set; }
    public virtual Payment? Payment { get; internal set; }

    public TimeSpan Duration => EndDateTime - StartDateTime;
    public bool IsActive => Status == BookingStatus.Confirmed || Status == BookingStatus.InProgress;

    // Required for EF Core + unit tests (InternalsVisibleTo)
    internal Booking()
    {
    }

    // ── Factories ─────────────────────────────────────────────────

    /// <summary>
    /// Marketplace booking request (starts as Pending until vendor approves).
    /// </summary>
    public static Booking CreateMarketplace(
        Guid userId,
        Guid parkingSpaceId,
        DateTime startDateTimeUtc,
        DateTime endDateTimeUtc,
        PricingType pricingType,
        VehicleType vehicleType,
        decimal baseAmount,
        decimal taxAmount,
        decimal serviceFee,
        decimal discountAmount,
        decimal totalAmount,
        string? discountCode = null,
        Guid? parkingPassId = null,
        int? slotNumber = null,
        string? vehicleNumber = null,
        string? vehicleModel = null,
        string? vehicleColor = null,
        string? bookingReference = null)
    {
        ValidatePartyAndWindow(userId, parkingSpaceId, startDateTimeUtc, endDateTimeUtc);
        ValidateAmounts(baseAmount, taxAmount, serviceFee, discountAmount, totalAmount);

        return new Booking
        {
            UserId = userId,
            ParkingSpaceId = parkingSpaceId,
            ParkingPassId = parkingPassId,
            StartDateTime = startDateTimeUtc,
            EndDateTime = endDateTimeUtc,
            PricingType = pricingType,
            VehicleType = vehicleType,
            SlotNumber = slotNumber,
            VehicleNumber = NormalizeOptional(vehicleNumber),
            VehicleModel = NormalizeOptional(vehicleModel),
            VehicleColor = NormalizeOptional(vehicleColor),
            BaseAmount = baseAmount,
            TaxAmount = taxAmount,
            ServiceFee = serviceFee,
            DiscountAmount = discountAmount,
            TotalAmount = totalAmount,
            DiscountCode = discountCode,
            Status = BookingStatus.Pending,
            BookingReference = string.IsNullOrWhiteSpace(bookingReference)
                ? GenerateReference("BK")
                : bookingReference.Trim()
        };
    }

    /// <summary>
    /// Corporate employee booking (confirmed at creation; slot may be assigned later).
    /// </summary>
    public static Booking CreateCorporateEmployee(
        Guid userId,
        Guid parkingSpaceId,
        DateTime startDateTimeUtc,
        DateTime endDateTimeUtc,
        VehicleType vehicleType,
        decimal totalAmount,
        string? vehicleNumber = null,
        string? bookingReference = null,
        string? qrCode = null)
    {
        ValidatePartyAndWindow(userId, parkingSpaceId, startDateTimeUtc, endDateTimeUtc);
        if (totalAmount < 0)
            throw new ValidationException("totalAmount", "Total amount cannot be negative");

        var booking = new Booking
        {
            UserId = userId,
            ParkingSpaceId = parkingSpaceId,
            StartDateTime = startDateTimeUtc,
            EndDateTime = endDateTimeUtc,
            PricingType = PricingType.Hourly,
            VehicleType = vehicleType,
            VehicleNumber = NormalizeOptional(vehicleNumber),
            BaseAmount = totalAmount,
            TaxAmount = 0,
            ServiceFee = 0,
            DiscountAmount = 0,
            TotalAmount = totalAmount,
            Status = BookingStatus.Confirmed,
            BookingReference = string.IsNullOrWhiteSpace(bookingReference)
                ? GenerateReference("CORP")
                : bookingReference.Trim(),
            QRCode = string.IsNullOrWhiteSpace(qrCode)
                ? $"CORP-{Guid.NewGuid():N}".ToUpperInvariant()
                : qrCode
        };

        booking.AddDomainEvent(new BookingConfirmedEvent(
            booking.Id, booking.UserId, booking.ParkingSpaceId, booking.BookingReference));
        return booking;
    }

    /// <summary>
    /// Corporate visitor booking (confirmed at creation; QR may be set from access policy later).
    /// </summary>
    public static Booking CreateCorporateVisitor(
        Guid userId,
        Guid parkingSpaceId,
        DateTime startDateTimeUtc,
        DateTime endDateTimeUtc,
        decimal totalAmount,
        string? visitorLicensePlate = null,
        string? bookingReference = null)
    {
        ValidatePartyAndWindow(userId, parkingSpaceId, startDateTimeUtc, endDateTimeUtc);
        if (totalAmount < 0)
            throw new ValidationException("totalAmount", "Total amount cannot be negative");

        var booking = new Booking
        {
            UserId = userId,
            ParkingSpaceId = parkingSpaceId,
            StartDateTime = startDateTimeUtc,
            EndDateTime = endDateTimeUtc,
            PricingType = PricingType.Hourly,
            VehicleType = VehicleType.Car,
            VehicleNumber = NormalizeOptional(visitorLicensePlate)?.ToUpperInvariant(),
            BaseAmount = totalAmount,
            TaxAmount = 0,
            ServiceFee = 0,
            DiscountAmount = 0,
            TotalAmount = totalAmount,
            Status = BookingStatus.Confirmed,
            BookingReference = string.IsNullOrWhiteSpace(bookingReference)
                ? GenerateReference("VIS")
                : bookingReference.Trim()
        };

        booking.AddDomainEvent(new BookingConfirmedEvent(
            booking.Id, booking.UserId, booking.ParkingSpaceId, booking.BookingReference));
        return booking;
    }

    // ── Lifecycle ─────────────────────────────────────────────────

    public void Confirm()
    {
        if (Status != BookingStatus.Pending && Status != BookingStatus.AwaitingPayment)
            throw new BusinessRuleException("Booking.Confirm", $"Cannot confirm booking in {Status} status");
        Status = BookingStatus.Confirmed;
        AddDomainEvent(new BookingConfirmedEvent(Id, UserId, ParkingSpaceId, BookingReference));
    }

    public void AwaitPayment()
    {
        if (Status != BookingStatus.Pending)
            throw new BusinessRuleException("Booking.AwaitPayment", $"Cannot set awaiting payment from {Status} status");
        Status = BookingStatus.AwaitingPayment;
    }

    public void Cancel(string reason)
    {
        if (Status == BookingStatus.Completed || Status == BookingStatus.Cancelled)
            throw new BusinessRuleException("Booking.Cancel", $"Cannot cancel booking in {Status} status");
        Status = BookingStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
        AddDomainEvent(new BookingCancelledEvent(Id, UserId, ParkingSpaceId, BookingReference, reason));
    }

    public void Reject(string reason)
    {
        if (Status != BookingStatus.Pending)
            throw new BusinessRuleException("Booking.Reject", "Can only reject pending bookings");
        Status = BookingStatus.Rejected;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
    }

    public void CheckIn()
    {
        if (Status != BookingStatus.Confirmed)
            throw new BusinessRuleException("Booking.CheckIn", $"Cannot check in booking in {Status} status");
        if (DateTime.UtcNow < StartDateTime.AddHours(-1))
            throw new BusinessRuleException("Booking.CheckInWindow", "Check-in is only allowed within 1 hour before start time");
        Status = BookingStatus.InProgress;
        CheckInTime = DateTime.UtcNow;
        AddDomainEvent(new BookingCheckedInEvent(Id, UserId, ParkingSpaceId, BookingReference));
    }

    public void CheckOut()
    {
        if (Status != BookingStatus.InProgress)
            throw new BusinessRuleException("Booking.CheckOut", $"Cannot check out booking in {Status} status");
        Status = BookingStatus.Completed;
        CheckOutTime = DateTime.UtcNow;
        AddDomainEvent(new BookingCheckedOutEvent(Id, UserId, ParkingSpaceId, BookingReference));
    }

    public void ApplyDiscount(string discountCode, decimal discountAmount)
    {
        if (Status != BookingStatus.Pending)
            throw new BusinessRuleException("Booking.ApplyDiscount", "Can only apply discount to pending bookings");
        if (discountAmount < 0 || discountAmount > BaseAmount)
            throw new ValidationException("discountAmount", "Invalid discount amount");
        DiscountCode = discountCode;
        DiscountAmount = discountAmount;
        TotalAmount = BaseAmount + TaxAmount + ServiceFee - DiscountAmount;
    }

    // ── Updates (marketplace edit) ────────────────────────────────

    public void UpdateVehicleDetails(VehicleType? vehicleType, string? vehicleNumber, string? vehicleModel, string? vehicleColor = null)
    {
        EnsureEditable();
        if (vehicleType.HasValue)
            VehicleType = vehicleType.Value;
        if (!string.IsNullOrWhiteSpace(vehicleNumber))
            VehicleNumber = vehicleNumber.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(vehicleModel))
            VehicleModel = vehicleModel.Trim();
        if (!string.IsNullOrWhiteSpace(vehicleColor))
            VehicleColor = vehicleColor.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reschedule(DateTime startDateTimeUtc, DateTime endDateTimeUtc)
    {
        EnsureEditable();
        if (endDateTimeUtc <= startDateTimeUtc)
            throw new BusinessRuleException("Booking.Reschedule", "End date must be after start date");
        StartDateTime = startDateTimeUtc;
        EndDateTime = endDateTimeUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyPricing(
        decimal baseAmount,
        decimal taxAmount,
        decimal serviceFee,
        decimal discountAmount,
        decimal totalAmount,
        Guid? parkingPassId,
        string? discountCode)
    {
        EnsureEditable();
        ValidateAmounts(baseAmount, taxAmount, serviceFee, discountAmount, totalAmount);
        BaseAmount = baseAmount;
        TaxAmount = taxAmount;
        ServiceFee = serviceFee;
        DiscountAmount = discountAmount;
        TotalAmount = totalAmount;
        ParkingPassId = parkingPassId;
        DiscountCode = discountCode;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignSlot(int? slotNumber)
    {
        if (slotNumber.HasValue && slotNumber.Value < 1)
            throw new ValidationException("slotNumber", "Slot number must be at least 1");
        SlotNumber = slotNumber;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetQrCode(string? qrCode)
    {
        QRCode = string.IsNullOrWhiteSpace(qrCode) ? null : qrCode.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Extension domain methods ──────────────────────────────────

    public void RequestExtension(DateTime newEndDateTime, decimal extraAmount)
    {
        if (Status != BookingStatus.Confirmed && Status != BookingStatus.InProgress)
            throw new BusinessRuleException("Booking.RequestExtension", "Only confirmed or in-progress bookings can be extended");
        if (newEndDateTime <= EndDateTime)
            throw new BusinessRuleException("Booking.RequestExtension", "New end time must be after the current end time");
        if (extraAmount < 0)
            throw new ValidationException("extraAmount", "Extra amount cannot be negative");

        PreExtensionStatus = Status;
        PendingExtensionEndDateTime = newEndDateTime;
        PendingExtensionAmount = extraAmount;
        Status = BookingStatus.PendingExtension;
    }

    public void ApproveExtension()
    {
        if (Status != BookingStatus.PendingExtension)
            throw new BusinessRuleException("Booking.ApproveExtension", "Only pending extension requests can be approved");
        Status = BookingStatus.AwaitingExtensionPayment;
    }

    public void RejectExtension(string reason)
    {
        if (Status != BookingStatus.PendingExtension)
            throw new BusinessRuleException("Booking.RejectExtension", "Only pending extension requests can be rejected");
        Status = PreExtensionStatus ?? BookingStatus.Confirmed;
        CancellationReason = null;
        PendingExtensionEndDateTime = null;
        PendingExtensionAmount = null;
        PreExtensionStatus = null;
    }

    public void ConfirmExtension()
    {
        if (Status != BookingStatus.AwaitingExtensionPayment && Status != BookingStatus.PendingExtension)
            throw new BusinessRuleException("Booking.ConfirmExtension", "Extension must be approved before it can be confirmed");
        if (!PendingExtensionEndDateTime.HasValue || !PendingExtensionAmount.HasValue)
            throw new BusinessRuleException("Booking.ConfirmExtension", "No pending extension to confirm");
        if (Status == BookingStatus.PendingExtension && PendingExtensionAmount.Value > 0)
            throw new BusinessRuleException("Booking.ConfirmExtension", "Extensions with a payment due must wait for payment confirmation");

        EndDateTime = PendingExtensionEndDateTime.Value;
        TotalAmount += PendingExtensionAmount.Value;
        Status = PreExtensionStatus ?? BookingStatus.Confirmed;
        PendingExtensionEndDateTime = null;
        PendingExtensionAmount = null;
        PreExtensionStatus = null;
    }

    // ── Helpers ───────────────────────────────────────────────────

    private void EnsureEditable()
    {
        if (Status != BookingStatus.Pending && Status != BookingStatus.Confirmed)
            throw new BusinessRuleException("Booking.Update", "Cannot update this booking");
    }

    private static void ValidatePartyAndWindow(Guid userId, Guid parkingSpaceId, DateTime start, DateTime end)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("userId", "User ID is required");
        if (parkingSpaceId == Guid.Empty)
            throw new ValidationException("parkingSpaceId", "Parking space ID is required");
        if (end <= start)
            throw new BusinessRuleException("Booking.Window", "End date must be after start date");
    }

    private static void ValidateAmounts(
        decimal baseAmount,
        decimal taxAmount,
        decimal serviceFee,
        decimal discountAmount,
        decimal totalAmount)
    {
        if (baseAmount < 0 || taxAmount < 0 || serviceFee < 0 || discountAmount < 0 || totalAmount < 0)
            throw new ValidationException("amounts", "Pricing amounts cannot be negative");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GenerateReference(string prefix) =>
        $"{prefix}{DateTime.UtcNow:yyyyMMdd}{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
