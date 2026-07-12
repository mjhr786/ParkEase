using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.CQRS.Commands.Corporate;

/// <summary>
/// Register a new company. The calling user becomes the first admin.
/// </summary>
public record CreateCompanyCommand(
    Guid UserId,
    CreateCompanyDto Dto
) : ICommand<ApiResponse<CompanyDto>>;

/// <summary>
/// Company admin updates profile fields (name, contact, billing).
/// </summary>
public record UpdateCompanyCommand(
    Guid CompanyId,
    Guid AdminUserId,
    UpdateCompanyDto Dto
) : ICommand<ApiResponse<CompanyDto>>;

/// <summary>
/// Cancel a pending employee invitation.
/// </summary>
public record CancelInvitationCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid InvitationId
) : ICommand<ApiResponse<bool>>;

/// <summary>
/// Resend (renew token + expiry) a pending or expired invitation.
/// </summary>
public record ResendInvitationCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid InvitationId
) : ICommand<ApiResponse<InvitationDto>>;

/// <summary>
/// Add an existing ParkEase user as a member of the company.
/// </summary>
public record AddMemberCommand(
    Guid CompanyId,
    Guid AdminUserId,
    AddMemberDto Dto
) : ICommand<ApiResponse<MembershipDto>>;

/// <summary>
/// Invite a new user by email to join the company.
/// </summary>
public record InviteMemberCommand(
    Guid CompanyId,
    Guid AdminUserId,
    InviteMemberDto Dto
) : ICommand<ApiResponse<InvitationDto>>;

/// <summary>
/// Accept an employee invitation by token.
/// </summary>
public record AcceptInvitationCommand(
    Guid UserId,
    string InvitationToken
) : ICommand<ApiResponse<MembershipDto>>;

/// <summary>
/// Remove (deactivate) a member from the company.
/// </summary>
public record RemoveMemberCommand(
    Guid CompanyId,
    Guid MembershipId,
    Guid AdminUserId
) : ICommand<ApiResponse<bool>>;

/// <summary>
/// Update role / priority / employee code for a company member.
/// </summary>
public record UpdateMemberCommand(
    Guid CompanyId,
    Guid MembershipId,
    Guid AdminUserId,
    UpdateMemberDto Dto
) : ICommand<ApiResponse<MembershipDto>>;

/// <summary>
/// Request parking slot allocation at a specific parking space.
/// Status is PendingApproval until parking space owner approves.
/// </summary>
public record AllocateParkingSlotsCommand(
    Guid CompanyId,
    Guid AdminUserId,
    AllocateParkingSlotsDto Dto
) : ICommand<ApiResponse<ParkingAllocationDto>>;

public record CreateCorporateParkingSpaceCommand(
    Guid CompanyId,
    Guid AdminUserId,
    CreateParkingSpaceDto Dto
) : ICommand<ApiResponse<CorporateParkingSpaceDto>>;

public record CreateOwnedParkingAllocationCommand(
    Guid CompanyId,
    Guid AdminUserId,
    CreateOwnedParkingAllocationDto Dto
) : ICommand<ApiResponse<ParkingAllocationDto>>;

public record ToggleCorporateParkingSpaceCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid ParkingSpaceId
) : ICommand<ApiResponse<CorporateParkingSpaceDto>>;

public record UpdateCorporateParkingSpaceCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid ParkingSpaceId,
    UpdateCorporateParkingSpaceDto Dto
) : ICommand<ApiResponse<CorporateParkingSpaceDto>>;

public record RetireCorporateParkingSpaceCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid ParkingSpaceId
) : ICommand<ApiResponse<bool>>;

/// <summary>
/// Parking space owner approves a pending allocation.
/// </summary>
public record ApproveAllocationCommand(
    Guid AllocationId,
    Guid ParkingOwnerUserId
) : ICommand<ApiResponse<ParkingAllocationDto>>;

/// <summary>
/// Parking space owner rejects a pending allocation.
/// </summary>
public record RejectAllocationCommand(
    Guid AllocationId,
    Guid ParkingOwnerUserId,
    string? Reason
) : ICommand<ApiResponse<ParkingAllocationDto>>;

/// <summary>
/// Book corporate parking for an employee.
/// </summary>
public record BookCorporateParkingCommand(
    Guid CompanyId,
    Guid UserId,
    BookCorporateParkingDto Dto
) : ICommand<ApiResponse<CorporateReservationResultDto>>;

/// <summary>
/// Book parking for a visitor (employee-initiated).
/// </summary>
public record BookVisitorParkingCommand(
    Guid CompanyId,
    Guid UserId,
    BookVisitorParkingDto Dto
) : ICommand<ApiResponse<CorporateReservationResultDto>>;

/// <summary>
/// Update the booking policy for a specific allocation.
/// </summary>
public record UpdateBookingPolicyCommand(
    Guid CompanyId,
    Guid AllocationId,
    Guid AdminUserId,
    BookingPolicyDto Policy
) : ICommand<ApiResponse<ParkingAllocationDto>>;

/// <summary>
/// Update lease/contract terms (rate, dates, lease reference) for an allocation.
/// </summary>
public record UpdateAllocationContractCommand(
    Guid CompanyId,
    Guid AllocationId,
    Guid AdminUserId,
    UpdateAllocationContractDto Dto
) : ICommand<ApiResponse<ParkingAllocationDto>>;

/// <summary>
/// Assign a fixed parking slot to a membership within an allocation.
/// </summary>
public record AssignFixedSlotCommand(
    Guid CompanyId,
    Guid AllocationId,
    Guid AdminUserId,
    AssignFixedSlotDto Dto
) : ICommand<ApiResponse<ParkingAllocationDto>>;

/// <summary>
/// Remove a fixed parking slot assignment from a membership within an allocation.
/// </summary>
public record RemoveFixedSlotCommand(
    Guid CompanyId,
    Guid AllocationId,
    Guid AdminUserId,
    Guid MembershipId
) : ICommand<ApiResponse<ParkingAllocationDto>>;

/// <summary>
/// Cancel a pending corporate waitlist entry.
/// </summary>
public record CancelWaitlistEntryCommand(
    Guid CompanyId,
    Guid UserId,
    Guid WaitlistEntryId
) : ICommand<ApiResponse<bool>>;

/// <summary>
/// Promote a pending waitlist entry into a confirmed corporate booking.
/// </summary>
public record PromoteWaitlistEntryCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid WaitlistEntryId
) : ICommand<ApiResponse<CorporateReservationResultDto>>;

/// <summary>
/// Cancel a corporate booking (employee: own only; company admin: any company booking).
/// </summary>
public record CancelCorporateBookingCommand(
    Guid CompanyId,
    Guid UserId,
    Guid BookingId,
    string Reason
) : ICommand<ApiResponse<CorporateBookingDto>>;

/// <summary>
/// Generate a draft corporate invoice for a billing period (admin only).
/// </summary>
public record GenerateCorporateInvoiceCommand(
    Guid CompanyId,
    Guid AdminUserId,
    GenerateCorporateInvoiceDto Dto
) : ICommand<ApiResponse<CorporateInvoiceDetailDto>>;

/// <summary>
/// Issue a draft invoice (admin only).
/// </summary>
public record IssueCorporateInvoiceCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid InvoiceId
) : ICommand<ApiResponse<CorporateInvoiceDetailDto>>;

/// <summary>
/// Mark an issued invoice as paid offline (admin only).
/// </summary>
public record MarkCorporateInvoicePaidCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid InvoiceId,
    MarkInvoicePaidDto Dto
) : ICommand<ApiResponse<CorporateInvoiceDetailDto>>;

/// <summary>
/// Void a draft or issued invoice (admin only).
/// </summary>
public record VoidCorporateInvoiceCommand(
    Guid CompanyId,
    Guid AdminUserId,
    Guid InvoiceId,
    VoidInvoiceDto Dto
) : ICommand<ApiResponse<CorporateInvoiceDetailDto>>;
