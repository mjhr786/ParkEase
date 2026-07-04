using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Queries.Corporate;
using ParkingApp.Application.DTOs;
using System.Security.Claims;

namespace ParkingApp.API.Controllers;

[ApiController]
[Route("api/v1/corporate")]
[Authorize] // Enforce JWT auth
public class CorporateController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public CorporateController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    private Guid GetUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    // ══════════════════════════════════════════════════════
    // COMPANIES
    // ══════════════════════════════════════════════════════

    [HttpPost("companies")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyDto dto)
    {
        var result = await _dispatcher.SendAsync(new CreateCompanyCommand(GetUserId(), dto));
            
        return result.Success ? Created($"/api/v1/corporate/companies/{result.Data?.Id}", result) : BadRequest(result);
    }

    [HttpGet("me/companies")]
    [ProducesResponseType(typeof(ApiResponse<List<CompanyDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCompanies()
    {
        var result = await _dispatcher.QueryAsync(new GetMyCompaniesQuery(GetUserId()));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("companies/{companyId}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompany([FromRoute] Guid companyId)
    {
        var result = await _dispatcher.QueryAsync(new GetCompanyDetailsQuery(companyId, GetUserId()));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("companies/{companyId}/dashboard")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard([FromRoute] Guid companyId)
    {
        var result = await _dispatcher.QueryAsync(new GetCompanyDashboardQuery(companyId, GetUserId()));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ══════════════════════════════════════════════════════
    // MEMBERSHIPS & INVITATIONS
    // ══════════════════════════════════════════════════════

    [HttpGet("companies/{companyId}/members")]
    [ProducesResponseType(typeof(ApiResponse<CompanyMembersDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers([FromRoute] Guid companyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _dispatcher.QueryAsync(new GetCompanyMembersQuery(companyId, GetUserId(), page, pageSize));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("companies/{companyId}/members")]
    [ProducesResponseType(typeof(ApiResponse<MembershipDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddMember([FromRoute] Guid companyId, [FromBody] AddMemberDto dto)
    {
        var result = await _dispatcher.SendAsync(new AddMemberCommand(companyId, GetUserId(), dto));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("companies/{companyId}/invitations")]
    [ProducesResponseType(typeof(ApiResponse<InvitationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> InviteMember([FromRoute] Guid companyId, [FromBody] InviteMemberDto dto)
    {
        var result = await _dispatcher.SendAsync(new InviteMemberCommand(companyId, GetUserId(), dto));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("invitations/accept")] // Cross-company endpoint
    [ProducesResponseType(typeof(ApiResponse<MembershipDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptInvitation([FromBody] string token)
    {
        var result = await _dispatcher.SendAsync(new AcceptInvitationCommand(GetUserId(), token));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("companies/{companyId}/members/{membershipId}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveMember([FromRoute] Guid companyId, [FromRoute] Guid membershipId)
    {
        var result = await _dispatcher.SendAsync(new RemoveMemberCommand(companyId, membershipId, GetUserId()));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ══════════════════════════════════════════════════════
    // ALLOCATIONS & POLICIES
    // ══════════════════════════════════════════════════════

    [HttpGet("companies/{companyId}/allocations")]
    [ProducesResponseType(typeof(ApiResponse<List<ParkingAllocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllocations([FromRoute] Guid companyId)
    {
        var result = await _dispatcher.QueryAsync(new GetCompanyAllocationsQuery(companyId, GetUserId()));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("vendor/allocations")]
    [ProducesResponseType(typeof(ApiResponse<List<VendorParkingAllocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVendorAllocations()
    {
        var result = await _dispatcher.QueryAsync(new GetVendorAllocationsQuery(GetUserId()));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("companies/{companyId}/allocations")]
    [ProducesResponseType(typeof(ApiResponse<ParkingAllocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestAllocation([FromRoute] Guid companyId, [FromBody] AllocateParkingSlotsDto dto)
    {
        var result = await _dispatcher.SendAsync(new AllocateParkingSlotsCommand(companyId, GetUserId(), dto));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("companies/{companyId}/parking-spaces")]
    [ProducesResponseType(typeof(ApiResponse<List<CorporateParkingSpaceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompanyParkingSpaces([FromRoute] Guid companyId)
    {
        var result = await _dispatcher.QueryAsync(new GetCompanyParkingSpacesQuery(companyId, GetUserId()));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("companies/{companyId}/parking-spaces")]
    [ProducesResponseType(typeof(ApiResponse<CorporateParkingSpaceDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCompanyParkingSpace([FromRoute] Guid companyId, [FromBody] CreateParkingSpaceDto dto)
    {
        var result = await _dispatcher.SendAsync(new CreateCorporateParkingSpaceCommand(companyId, GetUserId(), dto));

        return result.Success ? Created($"/api/v1/corporate/companies/{companyId}/parking-spaces/{result.Data?.Id}", result) : BadRequest(result);
    }

    [HttpPost("companies/{companyId}/parking-spaces/{parkingSpaceId}/toggle-active")]
    [ProducesResponseType(typeof(ApiResponse<CorporateParkingSpaceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ToggleCompanyParkingSpace([FromRoute] Guid companyId, [FromRoute] Guid parkingSpaceId)
    {
        var result = await _dispatcher.SendAsync(new ToggleCorporateParkingSpaceCommand(companyId, GetUserId(), parkingSpaceId));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("companies/{companyId}/parking-spaces/{parkingSpaceId}")]
    [ProducesResponseType(typeof(ApiResponse<CorporateParkingSpaceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCompanyParkingSpace([FromRoute] Guid companyId, [FromRoute] Guid parkingSpaceId, [FromBody] UpdateCorporateParkingSpaceDto dto)
    {
        var result = await _dispatcher.SendAsync(new UpdateCorporateParkingSpaceCommand(companyId, GetUserId(), parkingSpaceId, dto));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("companies/{companyId}/parking-spaces/{parkingSpaceId}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RetireCompanyParkingSpace([FromRoute] Guid companyId, [FromRoute] Guid parkingSpaceId)
    {
        var result = await _dispatcher.SendAsync(new RetireCorporateParkingSpaceCommand(companyId, GetUserId(), parkingSpaceId));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("companies/{companyId}/parking-spaces/{parkingSpaceId}/allocations")]
    [ProducesResponseType(typeof(ApiResponse<ParkingAllocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOwnedParkingAllocation([FromRoute] Guid companyId, [FromRoute] Guid parkingSpaceId, [FromBody] CreateOwnedParkingAllocationDto dto)
    {
        var payload = dto with { ParkingSpaceId = parkingSpaceId };
        var result = await _dispatcher.SendAsync(new CreateOwnedParkingAllocationCommand(companyId, GetUserId(), payload));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Usually called by Parking Space Owner (vendor side, but grouped here for convenience)
    [HttpPost("allocations/{allocationId}/approve")]
    [ProducesResponseType(typeof(ApiResponse<ParkingAllocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAllocation([FromRoute] Guid allocationId)
    {
        var result = await _dispatcher.SendAsync(new ApproveAllocationCommand(allocationId, GetUserId()));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("allocations/{allocationId}/reject")]
    [ProducesResponseType(typeof(ApiResponse<ParkingAllocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectAllocation([FromRoute] Guid allocationId, [FromBody] string reason)
    {
        var result = await _dispatcher.SendAsync(new RejectAllocationCommand(allocationId, GetUserId(), reason));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("companies/{companyId}/allocations/{allocationId}/policy")]
    [ProducesResponseType(typeof(ApiResponse<ParkingAllocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePolicy([FromRoute] Guid companyId, [FromRoute] Guid allocationId, [FromBody] BookingPolicyDto policyDto)
    {
        var result = await _dispatcher.SendAsync(new UpdateBookingPolicyCommand(companyId, allocationId, GetUserId(), policyDto));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("companies/{companyId}/allocations/{allocationId}/fixed-slots")]
    [ProducesResponseType(typeof(ApiResponse<ParkingAllocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignFixedSlot([FromRoute] Guid companyId, [FromRoute] Guid allocationId, [FromBody] AssignFixedSlotDto dto)
    {
        var result = await _dispatcher.SendAsync(new AssignFixedSlotCommand(companyId, allocationId, GetUserId(), dto));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("companies/{companyId}/allocations/{allocationId}/fixed-slots/{membershipId}")]
    [ProducesResponseType(typeof(ApiResponse<ParkingAllocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveFixedSlot([FromRoute] Guid companyId, [FromRoute] Guid allocationId, [FromRoute] Guid membershipId)
    {
        var result = await _dispatcher.SendAsync(new RemoveFixedSlotCommand(companyId, allocationId, GetUserId(), membershipId));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ══════════════════════════════════════════════════════
    // BOOKINGS
    // ══════════════════════════════════════════════════════

    [HttpGet("companies/{companyId}/bookings")]
    [ProducesResponseType(typeof(ApiResponse<MemberBookingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBookings([FromRoute] Guid companyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _dispatcher.QueryAsync(new GetMemberBookingsQuery(companyId, GetUserId(), page, pageSize));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("companies/{companyId}/waitlist")]
    [ProducesResponseType(typeof(ApiResponse<List<CorporateWaitlistDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWaitlist([FromRoute] Guid companyId)
    {
        var result = await _dispatcher.QueryAsync(new GetCompanyWaitlistQuery(companyId, GetUserId()));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("companies/{companyId}/waitlist/{waitlistEntryId}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelWaitlistEntry([FromRoute] Guid companyId, [FromRoute] Guid waitlistEntryId)
    {
        var result = await _dispatcher.SendAsync(new CancelWaitlistEntryCommand(companyId, GetUserId(), waitlistEntryId));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("companies/{companyId}/waitlist/{waitlistEntryId}/promote")]
    [ProducesResponseType(typeof(ApiResponse<CorporateReservationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PromoteWaitlistEntry([FromRoute] Guid companyId, [FromRoute] Guid waitlistEntryId)
    {
        var result = await _dispatcher.SendAsync(new PromoteWaitlistEntryCommand(companyId, GetUserId(), waitlistEntryId));

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("companies/{companyId}/bookings/employee")]
    [ProducesResponseType(typeof(ApiResponse<CorporateReservationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BookEmployeeParking([FromRoute] Guid companyId, [FromBody] BookCorporateParkingDto dto)
    {
        var result = await _dispatcher.SendAsync(new BookCorporateParkingCommand(companyId, GetUserId(), dto));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("companies/{companyId}/bookings/visitor")]
    [ProducesResponseType(typeof(ApiResponse<CorporateReservationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BookVisitorParking([FromRoute] Guid companyId, [FromBody] BookVisitorParkingDto dto)
    {
        var result = await _dispatcher.SendAsync(new BookVisitorParkingCommand(companyId, GetUserId(), dto));
            
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
