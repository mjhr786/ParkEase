using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Marketplace.Domain.ValueObjects;

namespace ParkingApp.Marketplace.Domain.Entities;

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
    public Guid? EventParkingPackageId { get; internal set; }

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

    /// <summary>When set, overstay notification already sent for this stay (idempotent).</summary>
    public DateTime? OverstayNotifiedAt { get; internal set; }

    /// <summary>
    /// When set, a pre-end session reminder was already sent for the current EndDateTime window.
    /// Cleared when an extension is confirmed so a later end can remind again.
    /// </summary>
    public DateTime? SessionEndRemindedAt { get; internal set; }

    /// <summary>Assessed overstay fee amount (included in TotalAmount when applied).</summary>
    public decimal OverstayFeeAmount { get; internal set; }

    /// <summary>Billable overstay minutes used for the last fee assessment.</summary>
    public int OverstayBillableMinutes { get; internal set; }

    /// <summary>When an overstay fee was last assessed / increased.</summary>
    public DateTime? OverstayFeeChargedAt { get; internal set; }

    /// <summary>Amount of overstay fee already paid via Stripe (can lag assessed fee if fee increases).</summary>
    public decimal OverstayFeePaidAmount { get; internal set; }

    /// <summary>Last successful overstay fee payment transaction id (Stripe PaymentIntent).</summary>
    public string? OverstayFeeTransactionId { get; internal set; }

    public DateTime? OverstayFeePaidAt { get; internal set; }

    /// <summary>Unpaid portion of assessed overstay fee.</summary>
    public decimal OverstayFeeOutstanding =>
        OverstayFeeAmount > OverstayFeePaidAmount
            ? OverstayFeeAmount - OverstayFeePaidAmount
            : 0m;

    /// <summary>When true, booking includes EV charging surcharge at create time.</summary>
    public bool IncludeEvCharging { get; internal set; }

    /// <summary>Locked EV charging fee included in TotalAmount (parking + charging session).</summary>
    public decimal EvChargingFeeAmount { get; internal set; }

    /// <summary>Assessed EV idle / charger-hogging fee after end + grace.</summary>
    public decimal EvIdleFeeAmount { get; internal set; }

    /// <summary>When EV idle fee was last assessed.</summary>
    public DateTime? EvIdleFeeChargedAt { get; internal set; }

    /// <summary>Assigned facility level for indoor bay guidance (e.g. P2).</summary>
    public string? FacilityLevel { get; internal set; }

    /// <summary>Assigned facility zone for indoor bay guidance (e.g. Blue).</summary>
    public string? FacilityZone { get; internal set; }

    /// <summary>Display bay label (e.g. B-14). May mirror SlotNumber.</summary>
    public string? BayLabel { get; internal set; }

    /// <summary>Valet vehicle retrieval status.</summary>
    public ValetStatus ValetStatus { get; internal set; } = ValetStatus.None;

    public DateTime? ValetRequestedAt { get; internal set; }
    public DateTime? ValetTargetReadyAt { get; internal set; }
    public DateTime? ValetReadyAt { get; internal set; }
    public DateTime? ValetStaffNotifiedAt { get; internal set; }
    public string? ValetNotes { get; internal set; }

    public string? CancellationReason { get; internal set; }
    public DateTime? CancelledAt { get; internal set; }
    public decimal? RefundAmount { get; internal set; }

    /// <summary>
    /// When true, this marketplace booking row was staged by Corporate (via Contracts).
    /// Consumer My Bookings list/detail/cancel must exclude these; vendor owner views may include them.
    /// Marketplace-owned flag - avoids SQL anti-join against CorporateBookings (KD-19).
    /// </summary>
    public bool IsCorporateStaged { get; internal set; }

    public DateTime? PendingExtensionEndDateTime { get; internal set; }
    public decimal? PendingExtensionAmount { get; internal set; }
    public bool HasPendingExtension => PendingExtensionEndDateTime.HasValue;

    public BookingStatus? PreExtensionStatus { get; internal set; }
    public virtual ParkingSpace ParkingSpace { get; internal set; } = null!;
    public virtual ParkingPass? ParkingPass { get; internal set; }
    public virtual Payment? Payment { get; internal set; }

    private readonly List<BookingAncillaryLine> _ancillaryLines = new();
    public virtual IReadOnlyCollection<BookingAncillaryLine> AncillaryLines => _ancillaryLines.AsReadOnly();

    /// <summary>Sum of snapshot ancillary line totals (included in BaseAmount at create).</summary>
    public decimal AncillarySubtotal =>
        Math.Round(_ancillaryLines.Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);

    public TimeSpan Duration => EndDateTime - StartDateTime;
    public bool IsActive => Status == BookingStatus.Confirmed || Status == BookingStatus.InProgress;

    // Required for EF Core + unit tests (InternalsVisibleTo)
    internal Booking()
    {
    }

    // G��G�� Factories G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

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
        string? bookingReference = null,
        bool includeEvCharging = false,
        decimal evChargingFeeAmount = 0m,
        Guid? eventParkingPackageId = null)
    {
        ValidatePartyAndWindow(userId, parkingSpaceId, startDateTimeUtc, endDateTimeUtc);
        ValidateAmounts(baseAmount, taxAmount, serviceFee, discountAmount, totalAmount);
        if (evChargingFeeAmount < 0)
            throw new ValidationException("evChargingFeeAmount", "EV charging fee cannot be negative");

        var booking = new Booking
        {
            UserId = userId,
            ParkingSpaceId = parkingSpaceId,
            ParkingPassId = parkingPassId,
            EventParkingPackageId = eventParkingPackageId,
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
                ? GenerateReference(eventParkingPackageId.HasValue ? "EVT" : "BK")
                : bookingReference.Trim(),
            IncludeEvCharging = includeEvCharging,
            EvChargingFeeAmount = includeEvCharging ? evChargingFeeAmount : 0m
        };

        booking.AddDomainEvent(new BookingRequestedEvent(
            booking.Id, booking.UserId, booking.ParkingSpaceId, booking.BookingReference));
        return booking;
    }

    /// <summary>
    /// Snapshot an ancillary catalog selection onto this booking (name/price locked).
    /// </summary>
    public BookingAncillaryLine AddAncillaryLine(
        string snapshotName,
        decimal unitPrice,
        int quantity = 1,
        Guid? serviceId = null)
    {
        var line = BookingAncillaryLine.Create(Id, snapshotName, unitPrice, quantity, serviceId);
        _ancillaryLines.Add(line);
        return line;
    }

    /// <summary>
    /// Prepaid event package purchase: fixed window + package price.
    /// Starts AwaitingPayment when total &gt; 0, otherwise Confirmed with access pass.
    /// </summary>
    public static Booking CreateFromEventPackage(
        Guid userId,
        EventParkingPackage package,
        VehicleType vehicleType,
        decimal taxAmount,
        decimal serviceFee,
        decimal totalAmount,
        string? vehicleNumber = null,
        string? vehicleModel = null,
        string? vehicleColor = null)
    {
        if (package is null)
            throw new ValidationException("package", "Event package is required");

        // Book parking access window (showtime ± early entry / late exit buffers).
        ValidatePartyAndWindow(userId, package.ParkingSpaceId, package.AccessStartUtc, package.AccessEndUtc);
        if (taxAmount < 0 || serviceFee < 0 || totalAmount < 0)
            throw new ValidationException("amounts", "Pricing amounts cannot be negative");

        var booking = new Booking
        {
            UserId = userId,
            ParkingSpaceId = package.ParkingSpaceId,
            EventParkingPackageId = package.Id,
            StartDateTime = package.AccessStartUtc,
            EndDateTime = package.AccessEndUtc,
            PricingType = PricingType.Hourly,
            VehicleType = vehicleType,
            VehicleNumber = NormalizeOptional(vehicleNumber),
            VehicleModel = NormalizeOptional(vehicleModel),
            VehicleColor = NormalizeOptional(vehicleColor),
            BaseAmount = package.PackagePrice,
            TaxAmount = taxAmount,
            ServiceFee = serviceFee,
            DiscountAmount = 0,
            TotalAmount = totalAmount,
            Status = totalAmount > 0 ? BookingStatus.AwaitingPayment : BookingStatus.Confirmed,
            BookingReference = GenerateReference("EVT")
        };

        if (booking.Status == BookingStatus.Confirmed)
        {
            booking.EnsureAccessPass();
            booking.AddDomainEvent(new BookingConfirmedEvent(
                booking.Id, booking.UserId, booking.ParkingSpaceId, booking.BookingReference));
        }
        else
        {
            booking.AddDomainEvent(new BookingRequestedEvent(
                booking.Id, booking.UserId, booking.ParkingSpaceId, booking.BookingReference));
            booking.AddDomainEvent(new BookingApprovedEvent(
                booking.Id, booking.UserId, booking.ParkingSpaceId, booking.BookingReference, RequiresPayment: true));
        }

        return booking;
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
            IsCorporateStaged = true,
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
        string? bookingReference = null,
        VehicleType vehicleType = VehicleType.Car)
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
            VehicleNumber = NormalizeOptional(visitorLicensePlate)?.ToUpperInvariant(),
            BaseAmount = totalAmount,
            TaxAmount = 0,
            ServiceFee = 0,
            DiscountAmount = 0,
            TotalAmount = totalAmount,
            Status = BookingStatus.Confirmed,
            IsCorporateStaged = true,
            BookingReference = string.IsNullOrWhiteSpace(bookingReference)
                ? GenerateReference("VIS")
                : bookingReference.Trim()
        };

        booking.EnsureAccessPass();
        booking.AddDomainEvent(new BookingConfirmedEvent(
            booking.Id, booking.UserId, booking.ParkingSpaceId, booking.BookingReference));
        return booking;
    }

    // G��G�� Lifecycle G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

    public void Confirm()
    {
        if (Status != BookingStatus.Pending && Status != BookingStatus.AwaitingPayment)
            throw new BusinessRuleException("Booking.Confirm", $"Cannot confirm booking in {Status} status");
        Status = BookingStatus.Confirmed;
        EnsureAccessPass();
        AddDomainEvent(new BookingConfirmedEvent(Id, UserId, ParkingSpaceId, BookingReference));
    }

    /// <summary>
    /// Issues a stable digital access-pass token (QR payload) if missing.
    /// Safe to call repeatedly; does not rotate an existing token.
    /// </summary>
    public bool EnsureAccessPass()
    {
        if (!string.IsNullOrWhiteSpace(QRCode))
            return false;

        // Scannable opaque token — not a secret by itself; gate verification re-checks booking state.
        var refPart = string.IsNullOrWhiteSpace(BookingReference)
            ? Id.ToString("N")[..8].ToUpperInvariant()
            : BookingReference.Trim().ToUpperInvariant();
        QRCode = $"PE-{refPart}-{Guid.NewGuid():N}".ToUpperInvariant();
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Whether the booking may be used for gate / QR access at the given UTC time.
    /// Window: Start − 1 hour through End (same entry window spirit as check-in).
    /// </summary>
    public bool IsAccessPassValidAt(DateTime asOfUtc)
    {
        if (string.IsNullOrWhiteSpace(QRCode))
            return false;

        if (Status is not (
            BookingStatus.Confirmed or
            BookingStatus.InProgress or
            BookingStatus.PendingExtension or
            BookingStatus.AwaitingExtensionPayment))
            return false;

        if (asOfUtc < StartDateTime.AddHours(-1))
            return false;

        if (EndDateTime > StartDateTime && asOfUtc >= EndDateTime)
            return false;

        return true;
    }

    public void AwaitPayment()
    {
        if (Status != BookingStatus.Pending)
            throw new BusinessRuleException("Booking.AwaitPayment", $"Cannot set awaiting payment from {Status} status");
        Status = BookingStatus.AwaitingPayment;
        AddDomainEvent(new BookingApprovedEvent(Id, UserId, ParkingSpaceId, BookingReference, RequiresPayment: true));
    }

    public void Cancel(string reason)
    {
        if (Status == BookingStatus.Completed || Status == BookingStatus.Cancelled)
            throw new BusinessRuleException("Booking.Cancel", $"Cannot cancel booking in {Status} status");
        Status = BookingStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
        // Drop stale extension state so cancelled bookings cannot be extended/paid.
        PendingExtensionEndDateTime = null;
        PendingExtensionAmount = null;
        PreExtensionStatus = null;
        AddDomainEvent(new BookingCancelledEvent(Id, UserId, ParkingSpaceId, BookingReference, reason));
    }

    public void Reject(string reason, Guid? vendorUserId = null)
    {
        if (Status != BookingStatus.Pending)
            throw new BusinessRuleException("Booking.Reject", "Can only reject pending bookings");
        Status = BookingStatus.Rejected;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
        AddDomainEvent(new BookingRejectedEvent(Id, UserId, ParkingSpaceId, BookingReference, reason, vendorUserId));
    }

    public void CheckIn() => CheckIn(DateTime.UtcNow);

    /// <summary>
    /// Check in using an explicit timestamp (e.g. LPR camera time).
    /// </summary>
    public void CheckIn(DateTime occurredAtUtc)
    {
        if (Status != BookingStatus.Confirmed)
            throw new BusinessRuleException("Booking.CheckIn", $"Cannot check in booking in {Status} status");
        if (occurredAtUtc < StartDateTime.AddHours(-1))
            throw new BusinessRuleException("Booking.CheckInWindow", "Check-in is only allowed within 1 hour before start time");
        // Only enforce end bound when a real window was set (EndDateTime > StartDateTime).
        if (EndDateTime > StartDateTime && occurredAtUtc >= EndDateTime)
            throw new BusinessRuleException("Booking.CheckInWindow", "Check-in is not allowed after the booking end time");

        Status = BookingStatus.InProgress;
        CheckInTime = occurredAtUtc;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new BookingCheckedInEvent(Id, UserId, ParkingSpaceId, BookingReference));
    }

    public void CheckOut() => CheckOut(DateTime.UtcNow);

    /// <summary>
    /// Check out using an explicit timestamp (e.g. LPR camera time).
    /// </summary>
    public void CheckOut(DateTime occurredAtUtc)
    {
        if (Status != BookingStatus.InProgress)
            throw new BusinessRuleException("Booking.CheckOut", $"Cannot check out booking in {Status} status");

        Status = BookingStatus.Completed;
        CheckOutTime = occurredAtUtc;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new BookingCheckedOutEvent(Id, UserId, ParkingSpaceId, BookingReference));
    }

    /// <summary>Records that an overstay alert was sent (once per stay).</summary>
    public bool TryMarkOverstayNotified(DateTime notifiedAtUtc)
    {
        if (Status != BookingStatus.InProgress)
            return false;
        if (OverstayNotifiedAt.HasValue)
            return false;

        OverstayNotifiedAt = notifiedAtUtc;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Records that a session-ending-soon reminder was sent (once per end window).
    /// Eligible when Confirmed or InProgress and not yet reminded.
    /// </summary>
    public bool TryMarkSessionEndReminded(DateTime remindedAtUtc)
    {
        if (Status is not (BookingStatus.Confirmed or BookingStatus.InProgress))
            return false;
        if (SessionEndRemindedAt.HasValue)
            return false;

        SessionEndRemindedAt = remindedAtUtc;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Applies or increases EV idle fee. Only increases TotalAmount by the delta.
    /// </summary>
    public bool ApplyEvIdleFee(decimal feeAmount, DateTime chargedAtUtc)
    {
        if (!IncludeEvCharging)
            return false;
        if (Status != BookingStatus.InProgress && Status != BookingStatus.Completed)
            throw new BusinessRuleException("Booking.EvIdleFee", $"Cannot assess EV idle fee in {Status} status");
        if (feeAmount < 0)
            throw new ValidationException("feeAmount", "EV idle fee cannot be negative");
        if (feeAmount <= EvIdleFeeAmount)
            return false;

        var delta = feeAmount - EvIdleFeeAmount;
        EvIdleFeeAmount = feeAmount;
        EvIdleFeeChargedAt = chargedAtUtc;
        TotalAmount += delta;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Settles or increases EV energy fee (kWh mode). Only increases TotalAmount by the delta.
    /// Hourly mode locks fee at create; PerKwh mode settles after charge stop.
    /// </summary>
    public bool ApplyEvEnergyFee(decimal feeAmount)
    {
        if (!IncludeEvCharging)
            return false;
        if (Status is not (BookingStatus.Confirmed or BookingStatus.InProgress or BookingStatus.Completed
            or BookingStatus.AwaitingPayment or BookingStatus.PendingExtension or BookingStatus.AwaitingExtensionPayment))
            throw new BusinessRuleException("Booking.EvEnergyFee", $"Cannot settle EV energy fee in {Status} status");
        if (feeAmount < 0)
            throw new ValidationException("feeAmount", "EV energy fee cannot be negative");
        if (feeAmount <= EvChargingFeeAmount)
            return false;

        var delta = feeAmount - EvChargingFeeAmount;
        // Adjust BaseAmount so tax base stays coherent for display; tax/service already on original quote.
        BaseAmount += delta;
        EvChargingFeeAmount = feeAmount;
        TotalAmount += delta;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Applies or increases overstay fee. Only increases TotalAmount by the delta.
    /// Returns false if amount is not greater than the fee already assessed.
    /// </summary>
    public bool ApplyOverstayFee(decimal feeAmount, int billableMinutes, DateTime chargedAtUtc)
    {
        if (Status != BookingStatus.InProgress && Status != BookingStatus.Completed)
            throw new BusinessRuleException("Booking.OverstayFee", $"Cannot assess overstay fee in {Status} status");
        if (feeAmount < 0)
            throw new ValidationException("feeAmount", "Overstay fee cannot be negative");
        if (billableMinutes < 0)
            throw new ValidationException("billableMinutes", "Billable minutes cannot be negative");

        // Only apply when there is a new positive fee or an increase.
        if (feeAmount <= OverstayFeeAmount)
            return false;

        var delta = feeAmount - OverstayFeeAmount;
        OverstayFeeAmount = feeAmount;
        OverstayBillableMinutes = billableMinutes;
        OverstayFeeChargedAt = chargedAtUtc;
        TotalAmount += delta;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new BookingOverstayFeeAssessedEvent(
            Id, UserId, ParkingSpaceId, BookingReference, feeAmount, billableMinutes, delta));
        return true;
    }

    /// <summary>
    /// Records a successful overstay fee payment. Does not change booking status.
    /// </summary>
    public void MarkOverstayFeePaid(decimal paidAmount, string? transactionId, DateTime paidAtUtc)
    {
        if (paidAmount <= 0)
            throw new ValidationException("paidAmount", "Paid amount must be positive");

        var outstanding = OverstayFeeOutstanding;
        if (outstanding <= 0)
            throw new BusinessRuleException("Booking.OverstayFeePaid", "No outstanding overstay fee to pay");

        // Accept payment up to outstanding (Stripe may bump to minimum charge).
        var applied = paidAmount >= outstanding ? outstanding : paidAmount;
        OverstayFeePaidAmount += applied;
        if (OverstayFeePaidAmount > OverstayFeeAmount)
            OverstayFeePaidAmount = OverstayFeeAmount;

        OverstayFeeTransactionId = string.IsNullOrWhiteSpace(transactionId) ? OverstayFeeTransactionId : transactionId.Trim();
        OverstayFeePaidAt = paidAtUtc;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new BookingOverstayFeePaidEvent(
            Id, UserId, ParkingSpaceId, BookingReference, applied, OverstayFeeOutstanding, OverstayFeeTransactionId));
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

    // G��G�� Updates (marketplace edit) G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

    public void UpdateVehicleDetails(VehicleType? vehicleType, string? vehicleNumber, string? vehicleModel, string? vehicleColor = null)
    {
        EnsureEditable();
        if (vehicleType.HasValue)
            VehicleType = vehicleType.Value;
        if (!string.IsNullOrWhiteSpace(vehicleNumber))
            VehicleNumber = LicensePlate.Normalize(vehicleNumber);
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

    /// <summary>
    /// Assigns indoor bay guidance labels (level / zone / bay). Optionally syncs SlotNumber.
    /// </summary>
    public void AssignBayGuidance(
        string? facilityLevel,
        string? facilityZone,
        string? bayLabel,
        int? slotNumber = null)
    {
        if (Status is BookingStatus.Cancelled or BookingStatus.Rejected or BookingStatus.Expired)
            throw new BusinessRuleException("Booking.AssignBay", $"Cannot assign bay in {Status} status");

        FacilityLevel = NormalizeBayLabel(facilityLevel, 32);
        FacilityZone = NormalizeBayLabel(facilityZone, 64);
        BayLabel = NormalizeBayLabel(bayLabel, 32);

        if (slotNumber.HasValue)
        {
            if (slotNumber.Value < 1)
                throw new ValidationException("slotNumber", "Slot number must be at least 1");
            SlotNumber = slotNumber;
            if (string.IsNullOrWhiteSpace(BayLabel))
                BayLabel = $"B-{slotNumber.Value}";
        }
        else if (SlotNumber.HasValue && string.IsNullOrWhiteSpace(BayLabel))
        {
            BayLabel = $"B-{SlotNumber.Value}";
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies facility defaults when bay guidance is enabled and no assignment exists yet.
    /// </summary>
    public bool TryAutoAssignBayFromFacility(
        bool bayGuidanceEnabled,
        string? defaultLevel,
        string? defaultZone,
        int? freeSlotNumber)
    {
        if (!bayGuidanceEnabled)
            return false;
        if (!string.IsNullOrWhiteSpace(BayLabel) || !string.IsNullOrWhiteSpace(FacilityLevel) || !string.IsNullOrWhiteSpace(FacilityZone))
            return false;

        var slot = SlotNumber ?? freeSlotNumber;
        if (!slot.HasValue && string.IsNullOrWhiteSpace(defaultLevel) && string.IsNullOrWhiteSpace(defaultZone))
            return false;

        AssignBayGuidance(defaultLevel, defaultZone, bayLabel: null, slotNumber: slot);
        return true;
    }

    public void RequestValet(DateTime requestedAtUtc, int leadMinutes, string? notes = null)
    {
        if (Status is not (BookingStatus.Confirmed or BookingStatus.InProgress
            or BookingStatus.PendingExtension or BookingStatus.AwaitingExtensionPayment))
            throw new BusinessRuleException("Booking.RequestValet", $"Cannot request valet in {Status} status");

        if (ValetStatus is ValetStatus.Requested or ValetStatus.InProgress or ValetStatus.Ready)
            throw new BusinessRuleException("Booking.RequestValet", "A valet request is already open for this booking");

        leadMinutes = Math.Clamp(leadMinutes, 1, 120);
        ValetStatus = ValetStatus.Requested;
        ValetRequestedAt = requestedAtUtc;
        ValetTargetReadyAt = requestedAtUtc.AddMinutes(leadMinutes);
        ValetReadyAt = null;
        ValetStaffNotifiedAt = null;
        ValetNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (ValetNotes is { Length: > 500 })
            throw new ValidationException("valetNotes", "Valet notes cannot exceed 500 characters");
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkValetStaffNotified(DateTime notifiedAtUtc)
    {
        if (ValetStatus != ValetStatus.Requested && ValetStatus != ValetStatus.InProgress)
            return;
        ValetStaffNotifiedAt = notifiedAtUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AcknowledgeValet()
    {
        if (ValetStatus != ValetStatus.Requested)
            throw new BusinessRuleException("Booking.AcknowledgeValet", "Only requested valet jobs can be acknowledged");
        ValetStatus = ValetStatus.InProgress;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkValetReady(DateTime readyAtUtc)
    {
        if (ValetStatus is not (ValetStatus.Requested or ValetStatus.InProgress))
            throw new BusinessRuleException("Booking.MarkValetReady", "Valet must be requested or in progress before ready");
        ValetStatus = ValetStatus.Ready;
        ValetReadyAt = readyAtUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CompleteValet()
    {
        if (ValetStatus is not (ValetStatus.Ready or ValetStatus.InProgress or ValetStatus.Requested))
            throw new BusinessRuleException("Booking.CompleteValet", "No open valet job to complete");
        ValetStatus = ValetStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CancelValet()
    {
        if (ValetStatus is not (ValetStatus.Requested or ValetStatus.InProgress or ValetStatus.Ready))
            throw new BusinessRuleException("Booking.CancelValet", "No open valet request to cancel");
        ValetStatus = ValetStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeBayLabel(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLen)
            throw new ValidationException("bayLabel", $"Value cannot exceed {maxLen} characters");
        return trimmed;
    }

    public void SetQrCode(string? qrCode)
    {
        QRCode = string.IsNullOrWhiteSpace(qrCode) ? null : qrCode.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    // G��G�� Extension domain methods G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

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
        AddDomainEvent(new BookingExtensionRequestedEvent(
            Id, UserId, ParkingSpaceId, BookingReference, newEndDateTime, extraAmount));
    }

    public void ApproveExtension(Guid? vendorUserId = null)
    {
        if (Status != BookingStatus.PendingExtension)
            throw new BusinessRuleException("Booking.ApproveExtension", "Only pending extension requests can be approved");
        Status = BookingStatus.AwaitingExtensionPayment;
        AddDomainEvent(new BookingExtensionApprovedEvent(
            Id,
            UserId,
            ParkingSpaceId,
            BookingReference,
            RequiresPayment: true,
            ExtraAmount: PendingExtensionAmount ?? 0m,
            NewEndUtc: PendingExtensionEndDateTime,
            VendorUserId: vendorUserId));
    }

    public void RejectExtension(string reason, Guid? vendorUserId = null)
    {
        if (Status != BookingStatus.PendingExtension)
            throw new BusinessRuleException("Booking.RejectExtension", "Only pending extension requests can be rejected");
        Status = PreExtensionStatus ?? BookingStatus.Confirmed;
        CancellationReason = null;
        PendingExtensionEndDateTime = null;
        PendingExtensionAmount = null;
        PreExtensionStatus = null;
        AddDomainEvent(new BookingExtensionRejectedEvent(
            Id, UserId, ParkingSpaceId, BookingReference, reason, vendorUserId));
    }

    public void ConfirmExtension()
    {
        if (Status != BookingStatus.AwaitingExtensionPayment && Status != BookingStatus.PendingExtension)
            throw new BusinessRuleException("Booking.ConfirmExtension", "Extension must be approved before it can be confirmed");
        if (!PendingExtensionEndDateTime.HasValue || !PendingExtensionAmount.HasValue)
            throw new BusinessRuleException("Booking.ConfirmExtension", "No pending extension to confirm");
        if (Status == BookingStatus.PendingExtension && PendingExtensionAmount.Value > 0)
            throw new BusinessRuleException("Booking.ConfirmExtension", "Extensions with a payment due must wait for payment confirmation");

        var newEnd = PendingExtensionEndDateTime.Value;
        var extra = PendingExtensionAmount.Value;
        EndDateTime = newEnd;
        TotalAmount += extra;
        Status = PreExtensionStatus ?? BookingStatus.Confirmed;
        PendingExtensionEndDateTime = null;
        PendingExtensionAmount = null;
        PreExtensionStatus = null;
        // New end window → allow another "ending soon" reminder before the new end.
        SessionEndRemindedAt = null;
        AddDomainEvent(new BookingExtensionConfirmedEvent(
            Id, UserId, ParkingSpaceId, BookingReference, newEnd, extra));
    }

    /// <summary>
    /// Raise payment-completed side-effect event after a successful charge (booking or extension).
    /// </summary>
    public void RecordPaymentCompleted(
        Guid paymentId,
        decimal amount,
        string currency,
        bool isExtensionPayment)
    {
        AddDomainEvent(new PaymentCompletedEvent(
            paymentId,
            Id,
            UserId,
            ParkingSpaceId,
            BookingReference,
            amount,
            currency,
            isExtensionPayment));
    }

    // G��G�� Helpers G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��G��

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

