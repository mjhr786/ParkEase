using System.ComponentModel.DataAnnotations;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.DTOs;

// ══════════════════════════════════════════════════════
// COMPANY DTOs
// ══════════════════════════════════════════════════════

public record CompanyDto(
    Guid Id,
    string Name,
    string RegistrationNumber,
    string ContactEmail,
    string ContactPhone,
    string BillingAddress,
    BillingType BillingType,
    bool IsActive,
    int MemberCount,
    int ActiveAllocationCount,
    DateTime CreatedAt);

public record CreateCompanyDto(
    [Required][StringLength(200, MinimumLength = 3)] string Name,
    [Required][StringLength(100)] string RegistrationNumber,
    [Required][EmailAddress] string ContactEmail,
    [Required][Phone] string ContactPhone,
    [Required][StringLength(500)] string BillingAddress,
    BillingType BillingType);

public record UpdateCompanyDto(
    [StringLength(200, MinimumLength = 3)] string? Name = null,
    [EmailAddress] string? ContactEmail = null,
    [Phone] string? ContactPhone = null,
    [StringLength(500)] string? BillingAddress = null,
    BillingType? BillingType = null);

// ══════════════════════════════════════════════════════
// MEMBERSHIP DTOs
// ══════════════════════════════════════════════════════

public record MembershipDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string UserEmail,
    CompanyRole Role,
    string? EmployeeCode,
    int Priority,
    bool IsActive,
    DateTime CreatedAt,
    Guid? CompanyId = null);

public record CompanyMembersDto(
    List<MembershipDto> Members,
    int TotalCount,
    int Page,
    int PageSize);

public record AddMemberDto(
    [Required][EmailAddress] string Email,
    CompanyRole Role = CompanyRole.Employee,
    string? EmployeeCode = null,
    [Range(1, 10)] int Priority = 1);

/// <param name="ClearEmployeeCode">When true, clears employee code even if EmployeeCode is null.</param>
public record UpdateMemberDto(
    CompanyRole? Role = null,
    [Range(1, 10)] int? Priority = null,
    string? EmployeeCode = null,
    bool ClearEmployeeCode = false);

// ══════════════════════════════════════════════════════
// INVITATION DTOs
// ══════════════════════════════════════════════════════

public record InviteMemberDto(
    [Required][EmailAddress] string Email,
    CompanyRole Role = CompanyRole.Employee);

/// <param name="InvitationToken">Included for company admins so they can share the accept link.</param>
public record InvitationDto(
    Guid Id,
    string Email,
    CompanyRole Role,
    InvitationStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    string? InvitationToken = null);

// ══════════════════════════════════════════════════════
// ALLOCATION DTOs
// ══════════════════════════════════════════════════════

public record ParkingAllocationDto(
    Guid Id,
    Guid CompanyId,
    Guid ParkingSpaceId,
    string ParkingSpaceTitle,
    int TotalSlots,
    int FixedSlots,
    int SharedSlots,
    decimal MonthlyRate,
    DateTime StartDate,
    DateTime EndDate,
    AllocationStatus Status,
    ParkingAllocationSource SourceType,
    Guid? VendorId,
    string? LeaseReference,
    Guid? ApprovedByUserId,
    DateTime? ApprovedAt,
    BookingPolicyDto? Policy,
    List<FixedSlotAssignmentDto> FixedAssignments,
    DateTime CreatedAt,
    string? VendorName = null);

public record UpdateAllocationContractDto(
    [Required][Range(0, 999999.99)] decimal MonthlyRate,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    [StringLength(100)] string? LeaseReference = null);

public record VendorParkingAllocationDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    Guid ParkingSpaceId,
    string ParkingSpaceTitle,
    int TotalSlots,
    int FixedSlots,
    int SharedSlots,
    decimal MonthlyRate,
    DateTime StartDate,
    DateTime EndDate,
    AllocationStatus Status,
    ParkingAllocationSource SourceType,
    Guid? VendorId,
    string? LeaseReference,
    Guid? ApprovedByUserId,
    DateTime? ApprovedAt,
    BookingPolicyDto? Policy,
    DateTime CreatedAt);

public record AllocateParkingSlotsDto(
    [Required] Guid ParkingSpaceId,
    [Required][Range(1, 1000)] int TotalSlots,
    [Range(0, 1000)] int FixedSlots,
    [Range(0, 1000)] int SharedSlots,
    [Required][Range(0, 999999.99)] decimal MonthlyRate,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    [StringLength(100)] string? LeaseReference,
    BookingPolicyDto? Policy);

public record CreateOwnedParkingAllocationDto(
    [Required] Guid ParkingSpaceId,
    [Required][Range(1, 1000)] int TotalSlots,
    [Range(0, 1000)] int FixedSlots,
    [Range(0, 1000)] int SharedSlots,
    [Required][Range(0, 999999.99)] decimal MonthlyRate,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    BookingPolicyDto? Policy);

public record CorporateParkingSpaceDto(
    Guid Id,
    Guid CompanyId,
    string Title,
    string Description,
    string Address,
    string City,
    string State,
    string Country,
    string PostalCode,
    double Latitude,
    double Longitude,
    ParkingType ParkingType,
    int TotalSpots,
    int AvailableSpots,
    decimal HourlyRate,
    decimal DailyRate,
    decimal WeeklyRate,
    decimal MonthlyRate,
    TimeSpan OpenTime,
    TimeSpan CloseTime,
    bool Is24Hours,
    List<string> Amenities,
    List<VehicleType> AllowedVehicleTypes,
    List<string> ImageUrls,
    bool IsActive,
    bool IsVerified,
    string? SpecialInstructions,
    string? ZoneCode,
    DateTime CreatedAt);

public record UpdateCorporateParkingSpaceDto(
    string? Title,
    string? Description,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? PostalCode,
    double? Latitude,
    double? Longitude,
    ParkingType? ParkingType,
    int? TotalSpots,
    decimal? HourlyRate,
    decimal? DailyRate,
    decimal? WeeklyRate,
    decimal? MonthlyRate,
    TimeSpan? OpenTime,
    TimeSpan? CloseTime,
    bool? Is24Hours,
    List<string>? Amenities,
    List<VehicleType>? AllowedVehicleTypes,
    List<string>? ImageUrls,
    string? SpecialInstructions,
    string? ZoneCode = null);

public record BookingPolicyDto(
    [Range(1, 100)] int MaxBookingsPerEmployeePerDay = 1,
    [Range(1, 500)] int MaxBookingsPerEmployeePerWeek = 5,
    [Range(1, 10)] int PriorityThreshold = 1,
    TimeSpan? AllowedStartTime = null,
    TimeSpan? AllowedEndTime = null,
    bool AllowWeekends = false);

public record FixedSlotAssignmentDto(
    Guid MembershipId,
    string UserName,
    int SlotNumber,
    DateTime AssignedAt);

public record AssignFixedSlotDto(
    [Required] Guid MembershipId,
    [Required][Range(1, 1000)] int SlotNumber);

// ══════════════════════════════════════════════════════
// CORPORATE BOOKING DTOs
// ══════════════════════════════════════════════════════

public record CorporateBookingDto(
    Guid Id,
    Guid BookingId,
    string? BookingReference,
    CorporateSlotType SlotType,
    int? SlotNumber,
    bool IsVisitorBooking,
    string? VisitorName,
    string? VisitorLicensePlate,
    DateTime StartDateTime,
    DateTime EndDateTime,
    BookingStatus BookingStatus,
    string? QrCodeToken,
    DateTime CreatedAt,
    Guid? AllocationId = null,
    string? ParkingSpaceTitle = null,
    Guid? MembershipId = null,
    string? MemberName = null,
    string? MemberEmail = null,
    decimal TotalAmount = 0,
    string? VehicleNumber = null);

/// <summary>Optional filters for corporate booking list/export.</summary>
public record CorporateBookingListFilter(
    BookingStatus? Status = null,
    bool? IsVisitor = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

public record CorporateWaitlistDto(
    Guid Id,
    Guid AllocationId,
    bool IsVisitorBooking,
    DateTime RequestedStartDateTime,
    DateTime RequestedEndDateTime,
    string? VehicleNumber,
    string? VisitorName,
    string? VisitorLicensePlate,
    WaitlistStatus Status,
    int PriorityAtRequest,
    int Position,
    DateTime CreatedAt);

public record FraudAssessmentDto(
    CorporateFraudRiskLevel RiskLevel,
    bool IsBlocked,
    string? Reason);

public record CorporateReservationResultDto(
    CorporateBookingDto? Booking,
    CorporateWaitlistDto? Waitlist,
    FraudAssessmentDto FraudAssessment);

public record BookCorporateParkingDto(
    [Required] Guid AllocationId,
    [Required] DateTime StartDateTime,
    [Required] DateTime EndDateTime,
    VehicleType VehicleType = VehicleType.Car,
    string? VehicleNumber = null);

public record BookVisitorParkingDto(
    [Required] Guid AllocationId,
    [Required] DateTime StartDateTime,
    [Required] DateTime EndDateTime,
    [Required][StringLength(200, MinimumLength = 2)] string VisitorName = "",
    [Required][StringLength(20, MinimumLength = 3)] string VisitorLicensePlate = "",
    [Required] DateTime AccessExpiry = default);

// ══════════════════════════════════════════════════════
// DASHBOARD DTOs
// ══════════════════════════════════════════════════════

public record CompanyDashboardDto(
    int TotalMembers,
    int ActiveMembers,
    int TotalAllocations,
    int ActiveAllocations,
    int OwnedParkingSpaces,
    int OwnedParkingSlots,
    int LeasedAllocations,
    int PendingVendorAllocations,
    int TotalBookingsThisMonth,
    int VisitorBookingsThisMonth,
    decimal TotalHoursUsedThisMonth,
    decimal MonthlySpend,
    double UtilizationPercentage,
    List<DashboardChartDataDto> BookingsByDay,
    List<AllocationUtilizationDto> AllocationBreakdown,
    int ActiveWaitlistEntries,
    int SuspiciousActivityCount,
    List<PeakHourDto> PeakHours,
    List<FraudAlertDto> FraudAlerts,
    int ExpiringAllocationsWithin30Days = 0,
    List<ExpiringAllocationDto>? ExpiringAllocations = null);

public record ExpiringAllocationDto(
    Guid AllocationId,
    string ParkingSpaceTitle,
    DateTime EndDate,
    string? LeaseReference,
    ParkingAllocationSource SourceType,
    decimal MonthlyRate);

public record AllocationUtilizationDto(
    Guid AllocationId,
    string ParkingSpaceTitle,
    int TotalSlots,
    int UsedToday,
    double UtilizationPercent);

public record PeakHourDto(
    int HourOfDay,
    int BookingCount);

public record FraudAlertDto(
    Guid MembershipId,
    string UserName,
    int Priority,
    int OverlappingBookingPairs,
    int RiskScore);

public record MemberBookingsDto(
    List<CorporateBookingDto> Bookings,
    int TotalCount,
    int Page,
    int PageSize);
