using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Queries.Corporate;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.UnitTests.API;

public class CorporateControllerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly CorporateController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    public CorporateControllerTests()
    {
        _controller = new CorporateController(_dispatcher.Object);
        SetUser(_userId);
    }

    private void SetUser(Guid userId)
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "mock"))
            }
        };
    }

    [Fact]
    public async Task CreateCompany_WhenSuccess_ReturnsCreated()
    {
        var company = new CompanyDto(
            _companyId, "Acme", "REG-1", "a@acme.com", "555", "Addr", BillingType.UsageBased, true, 0, 0, DateTime.UtcNow);
        var response = new ApiResponse<CompanyDto>(true, null, company);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateCompanyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var dto = new CreateCompanyDto("Acme", "REG-1", "a@acme.com", "555", "Addr", BillingType.UsageBased);
        var result = await _controller.CreateCompany(dto);

        result.Should().BeOfType<CreatedResult>().Which.Value.Should().Be(response);
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<CreateCompanyCommand>(c => c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCompany_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateCompanyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CompanyDto>(false, "invalid", null));

        var dto = new CreateCompanyDto("Acme", "REG-1", "a@acme.com", "555", "Addr", BillingType.ReservedSlots);
        var result = await _controller.CreateCompany(dto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetMyCompanies_WhenSuccess_ReturnsOk()
    {
        var response = new ApiResponse<List<CompanyDto>>(true, null, new List<CompanyDto>());
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetMyCompaniesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetMyCompanies();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetCompany_WhenSuccess_ReturnsOk()
    {
        var company = new CompanyDto(
            _companyId, "Acme", "REG-1", "a@acme.com", "555", "Addr", BillingType.UsageBased, true, 1, 1, DateTime.UtcNow);
        var response = new ApiResponse<CompanyDto>(true, null, company);
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCompanyDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetCompany(_companyId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetDashboard_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCompanyDashboardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CompanyDashboardDto>(false, "forbidden", null));

        var result = await _controller.GetDashboard(_companyId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetMembers_WhenSuccess_ReturnsOk()
    {
        var response = new ApiResponse<CompanyMembersDto>(
            true, null, new CompanyMembersDto(new List<MembershipDto>(), 0, 1, 50));
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCompanyMembersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetMembers(_companyId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAllocations_WhenSuccess_ReturnsOk()
    {
        var response = new ApiResponse<List<ParkingAllocationDto>>(true, null, new List<ParkingAllocationDto>());
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCompanyAllocationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetAllocations(_companyId);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetWaitlist_WhenSuccess_ReturnsOk()
    {
        var response = new ApiResponse<List<CorporateWaitlistDto>>(true, null, new List<CorporateWaitlistDto>());
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCompanyWaitlistQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetWaitlist(_companyId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateCompany_WhenSuccess_ReturnsOk()
    {
        var company = new CompanyDto(
            _companyId, "Acme2", "REG-1", "a@acme.com", "555", "Addr", BillingType.UsageBased, true, 0, 0, DateTime.UtcNow);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<UpdateCompanyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CompanyDto>(true, null, company));

        var result = await _controller.UpdateCompany(_companyId, new UpdateCompanyDto(Name: "Acme2"));

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── Parking-space / allocation policy routes (Wave 18 residual) ──

    [Fact]
    public async Task GetCompanyParkingSpaces_WhenSuccess_ReturnsOk()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCompanyParkingSpacesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<List<CorporateParkingSpaceDto>>(true, null, new List<CorporateParkingSpaceDto>()));

        var result = await _controller.GetCompanyParkingSpaces(_companyId);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.QueryAsync(
            It.Is<GetCompanyParkingSpacesQuery>(q => q.CompanyId == _companyId && q.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCompanyParkingSpace_WhenSuccess_ReturnsCreated()
    {
        var spaceId = Guid.NewGuid();
        var space = SampleParkingSpace(spaceId);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateCorporateParkingSpaceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateParkingSpaceDto>(true, null, space));

        var dto = SampleCreateParkingDto();
        var result = await _controller.CreateCompanyParkingSpace(_companyId, dto);

        result.Should().BeOfType<CreatedResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<CreateCorporateParkingSpaceCommand>(c => c.CompanyId == _companyId && c.AdminUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCompanyParkingSpace_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateCorporateParkingSpaceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateParkingSpaceDto>(false, "not admin", null));

        var result = await _controller.CreateCompanyParkingSpace(_companyId, SampleCreateParkingDto());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ToggleCompanyParkingSpace_WhenSuccess_ReturnsOk()
    {
        var spaceId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<ToggleCorporateParkingSpaceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateParkingSpaceDto>(true, null, SampleParkingSpace(spaceId)));

        var result = await _controller.ToggleCompanyParkingSpace(_companyId, spaceId);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<ToggleCorporateParkingSpaceCommand>(c =>
                c.CompanyId == _companyId && c.ParkingSpaceId == spaceId && c.AdminUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCompanyParkingSpace_WhenSuccess_ReturnsOk()
    {
        var spaceId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<UpdateCorporateParkingSpaceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateParkingSpaceDto>(true, null, SampleParkingSpace(spaceId)));

        var dto = new UpdateCorporateParkingSpaceDto(
            Title: "Updated", Description: null, Address: null, City: null, State: null,
            Country: null, PostalCode: null, Latitude: null, Longitude: null, ParkingType: null,
            TotalSpots: null, HourlyRate: null, DailyRate: null, WeeklyRate: null, MonthlyRate: null,
            OpenTime: null, CloseTime: null, Is24Hours: null, Amenities: null,
            AllowedVehicleTypes: null, ImageUrls: null, SpecialInstructions: null);

        var result = await _controller.UpdateCompanyParkingSpace(_companyId, spaceId, dto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RetireCompanyParkingSpace_WhenSuccess_ReturnsOk()
    {
        var spaceId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<RetireCorporateParkingSpaceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, null, true));

        var result = await _controller.RetireCompanyParkingSpace(_companyId, spaceId);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<RetireCorporateParkingSpaceCommand>(c =>
                c.CompanyId == _companyId && c.ParkingSpaceId == spaceId && c.AdminUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOwnedParkingAllocation_WhenSuccess_ReturnsOk()
    {
        var spaceId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var allocation = SampleAllocation(allocationId, spaceId);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateOwnedParkingAllocationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ParkingAllocationDto>(true, null, allocation));

        var dto = new CreateOwnedParkingAllocationDto(
            ParkingSpaceId: Guid.Empty, // controller overwrites from route
            TotalSlots: 10,
            FixedSlots: 2,
            SharedSlots: 8,
            MonthlyRate: 100m,
            StartDate: DateTime.UtcNow.Date,
            EndDate: DateTime.UtcNow.Date.AddMonths(6),
            Policy: null);

        var result = await _controller.CreateOwnedParkingAllocation(_companyId, spaceId, dto);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<CreateOwnedParkingAllocationCommand>(c =>
                c.CompanyId == _companyId &&
                c.AdminUserId == _userId &&
                c.Dto.ParkingSpaceId == spaceId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAllocation_WhenSuccess_ReturnsOk()
    {
        var allocationId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<ApproveAllocationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ParkingAllocationDto>(true, null, SampleAllocation(allocationId, Guid.NewGuid())));

        var result = await _controller.ApproveAllocation(allocationId);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<ApproveAllocationCommand>(c => c.AllocationId == allocationId && c.ParkingOwnerUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectAllocation_WhenFails_ReturnsBadRequest()
    {
        var allocationId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<RejectAllocationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ParkingAllocationDto>(false, "already decided", null));

        var result = await _controller.RejectAllocation(allocationId, "no capacity");

        result.Should().BeOfType<BadRequestObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<RejectAllocationCommand>(c =>
                c.AllocationId == allocationId && c.ParkingOwnerUserId == _userId && c.Reason == "no capacity"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePolicy_WhenSuccess_ReturnsOk()
    {
        var allocationId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<UpdateBookingPolicyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ParkingAllocationDto>(true, null, SampleAllocation(allocationId, Guid.NewGuid())));

        var policy = new BookingPolicyDto(MaxBookingsPerEmployeePerDay: 2, AllowWeekends: true);
        var result = await _controller.UpdatePolicy(_companyId, allocationId, policy);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AssignFixedSlot_WhenSuccess_ReturnsOk()
    {
        var allocationId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<AssignFixedSlotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ParkingAllocationDto>(true, null, SampleAllocation(allocationId, Guid.NewGuid())));

        var result = await _controller.AssignFixedSlot(
            _companyId, allocationId, new AssignFixedSlotDto(membershipId, 3));

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<AssignFixedSlotCommand>(c =>
                c.CompanyId == _companyId &&
                c.AllocationId == allocationId &&
                c.AdminUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveFixedSlot_WhenSuccess_ReturnsOk()
    {
        var allocationId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<RemoveFixedSlotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ParkingAllocationDto>(true, null, SampleAllocation(allocationId, Guid.NewGuid())));

        var result = await _controller.RemoveFixedSlot(_companyId, allocationId, membershipId);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<RemoveFixedSlotCommand>(c =>
                c.CompanyId == _companyId &&
                c.AllocationId == allocationId &&
                c.MembershipId == membershipId &&
                c.AdminUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Invite / book / invoice / export residual (Wave 19) ──

    [Fact]
    public async Task InviteMember_WhenSuccess_ReturnsOk()
    {
        var invitation = new InvitationDto(
            Guid.NewGuid(), "new@acme.com", CompanyRole.Employee, InvitationStatus.Pending,
            DateTime.UtcNow.AddDays(7), DateTime.UtcNow, "token-1");
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<InviteMemberCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<InvitationDto>(true, null, invitation));

        var result = await _controller.InviteMember(
            _companyId, new InviteMemberDto("new@acme.com", CompanyRole.Employee));

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<InviteMemberCommand>(c => c.CompanyId == _companyId && c.AdminUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelInvitation_WhenSuccess_ReturnsOk()
    {
        var invitationId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CancelInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, null, true));

        var result = await _controller.CancelInvitation(_companyId, invitationId);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<CancelInvitationCommand>(c =>
                c.CompanyId == _companyId && c.InvitationId == invitationId && c.AdminUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendInvitation_WhenFails_ReturnsBadRequest()
    {
        var invitationId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<ResendInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<InvitationDto>(false, "expired", null));

        var result = await _controller.ResendInvitation(_companyId, invitationId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AcceptInvitation_WhenSuccess_ReturnsOk()
    {
        var membership = new MembershipDto(
            Guid.NewGuid(), _userId, "User", "u@test.com", CompanyRole.Employee,
            null, 1, true, DateTime.UtcNow, _companyId);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<AcceptInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<MembershipDto>(true, null, membership));

        var result = await _controller.AcceptInvitation("invite-token");

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<AcceptInvitationCommand>(c => c.UserId == _userId && c.InvitationToken == "invite-token"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInvitations_WhenSuccess_ReturnsOk()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCompanyInvitationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<List<InvitationDto>>(true, null, new List<InvitationDto>()));

        var result = await _controller.GetInvitations(_companyId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task BookEmployeeParking_WhenSuccess_ReturnsOk()
    {
        var allocationId = Guid.NewGuid();
        var booking = SampleBooking(isVisitor: false);
        var reservation = new CorporateReservationResultDto(
            booking,
            null,
            new FraudAssessmentDto(CorporateFraudRiskLevel.None, false, null));
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<BookCorporateParkingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateReservationResultDto>(true, null, reservation));

        var dto = new BookCorporateParkingDto(
            allocationId,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            VehicleType.Car,
            "MH12AB1234");
        var result = await _controller.BookEmployeeParking(_companyId, dto);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<BookCorporateParkingCommand>(c => c.CompanyId == _companyId && c.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookVisitorParking_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<BookVisitorParkingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateReservationResultDto>(false, "no capacity", null));

        var dto = new BookVisitorParkingDto(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2),
            "Guest",
            "MH01XY9999",
            DateTime.UtcNow.AddHours(2));
        var result = await _controller.BookVisitorParking(_companyId, dto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CancelCorporateBooking_WhenSuccess_ReturnsOk()
    {
        var bookingId = Guid.NewGuid();
        var booking = SampleBooking(isVisitor: false) with { BookingId = bookingId };
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CancelCorporateBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateBookingDto>(true, null, booking));

        var result = await _controller.CancelCorporateBooking(
            _companyId, bookingId, new CancelBookingDto("plans changed"));

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<CancelCorporateBookingCommand>(c =>
                c.CompanyId == _companyId &&
                c.UserId == _userId &&
                c.BookingId == bookingId &&
                c.Reason == "plans changed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelCorporateBooking_WhenDtoNull_UsesDefaultReason()
    {
        var bookingId = Guid.NewGuid();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CancelCorporateBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateBookingDto>(true, null, SampleBooking(false)));

        await _controller.CancelCorporateBooking(_companyId, bookingId, null);

        _dispatcher.Verify(d => d.SendAsync(
            It.Is<CancelCorporateBookingCommand>(c => c.Reason == "Cancelled"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBookings_WhenSuccess_ReturnsOk()
    {
        var response = new ApiResponse<MemberBookingsDto>(
            true, null, new MemberBookingsDto(new List<CorporateBookingDto>(), 0, 1, 20));
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetMemberBookingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetBookings(_companyId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExportBookings_WhenSuccess_ReturnsCsvFile()
    {
        var bookings = new List<CorporateBookingDto>
        {
            SampleBooking(isVisitor: false),
            SampleBooking(isVisitor: true)
        };
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetMemberBookingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<MemberBookingsDto>(
                true, null, new MemberBookingsDto(bookings, 2, 1, 5000)));

        var result = await _controller.ExportBookings(_companyId);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        file.FileDownloadName.Should().Contain("corporate-bookings-");
        System.Text.Encoding.UTF8.GetString(file.FileContents).Should().Contain("Reference");
    }

    [Fact]
    public async Task ExportBookings_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetMemberBookingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<MemberBookingsDto>(false, "forbidden", null));

        var result = await _controller.ExportBookings(_companyId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExportDashboard_WhenSuccess_ReturnsCsvFile()
    {
        var dashboard = SampleDashboard();
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCompanyDashboardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CompanyDashboardDto>(true, null, dashboard));

        var result = await _controller.ExportDashboard(_companyId);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        file.FileDownloadName.Should().Contain("corporate-dashboard-");
        var csv = System.Text.Encoding.UTF8.GetString(file.FileContents);
        csv.Should().Contain("TotalMembers");
        csv.Should().Contain("BookingsByDay");
    }

    [Fact]
    public async Task ExportDashboard_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCompanyDashboardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CompanyDashboardDto>(false, "forbidden", null));

        var result = await _controller.ExportDashboard(_companyId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GenerateInvoice_WhenSuccess_ReturnsCreated()
    {
        var invoice = SampleInvoice();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<GenerateCorporateInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateInvoiceDetailDto>(true, null, invoice));

        var dto = new GenerateCorporateInvoiceDto(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-30)), DateOnly.FromDateTime(DateTime.UtcNow.Date));
        var result = await _controller.GenerateInvoice(_companyId, dto);

        result.Should().BeOfType<CreatedResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<GenerateCorporateInvoiceCommand>(c => c.CompanyId == _companyId && c.AdminUserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateInvoice_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<GenerateCorporateInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateInvoiceDetailDto>(false, "no data", null));

        var dto = new GenerateCorporateInvoiceDto(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-7)), DateOnly.FromDateTime(DateTime.UtcNow.Date));
        var result = await _controller.GenerateInvoice(_companyId, dto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetInvoices_WhenSuccess_ReturnsOk()
    {
        var list = new CorporateInvoiceListDto(new List<CorporateInvoiceSummaryDto>(), 0, 1, 20);
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCompanyInvoicesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateInvoiceListDto>(true, null, list));

        var result = await _controller.GetInvoices(_companyId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetInvoice_WhenSuccess_ReturnsOk()
    {
        var invoice = SampleInvoice();
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCorporateInvoiceDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateInvoiceDetailDto>(true, null, invoice));

        var result = await _controller.GetInvoice(_companyId, invoice.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task IssueInvoice_WhenSuccess_ReturnsOk()
    {
        var invoice = SampleInvoice();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<IssueCorporateInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateInvoiceDetailDto>(true, null, invoice));

        var result = await _controller.IssueInvoice(_companyId, invoice.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task MarkInvoicePaid_WhenDtoNull_SendsEmptyPaidDto()
    {
        var invoice = SampleInvoice();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<MarkCorporateInvoicePaidCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateInvoiceDetailDto>(true, null, invoice));

        var result = await _controller.MarkInvoicePaid(_companyId, invoice.Id, null);

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<MarkCorporateInvoicePaidCommand>(c =>
                c.CompanyId == _companyId &&
                c.InvoiceId == invoice.Id &&
                c.AdminUserId == _userId &&
                c.Dto != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VoidInvoice_WhenSuccess_ReturnsOk()
    {
        var invoice = SampleInvoice();
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<VoidCorporateInvoiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateInvoiceDetailDto>(true, null, invoice));

        var result = await _controller.VoidInvoice(
            _companyId, invoice.Id, new VoidInvoiceDto("duplicate"));

        result.Should().BeOfType<OkObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(
            It.Is<VoidCorporateInvoiceCommand>(c =>
                c.InvoiceId == invoice.Id && c.Dto.Reason == "duplicate"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportInvoice_WhenSuccess_ReturnsCsvFile()
    {
        var invoice = SampleInvoice();
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCorporateInvoiceDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateInvoiceDetailDto>(true, null, invoice));

        var result = await _controller.ExportInvoice(_companyId, invoice.Id);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        file.FileDownloadName.Should().Contain(invoice.InvoiceNumber);
        System.Text.Encoding.UTF8.GetString(file.FileContents).Should().Contain("InvoiceNumber");
    }

    [Fact]
    public async Task ExportInvoice_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetCorporateInvoiceDetailsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporateInvoiceDetailDto>(false, "not found", null));

        var result = await _controller.ExportInvoice(_companyId, Guid.NewGuid());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static CreateParkingSpaceDto SampleCreateParkingDto() =>
        new(
            Title: "Lot A",
            Description: "Company lot",
            Address: "1 Main",
            City: "Mumbai",
            State: "MH",
            Country: "IN",
            PostalCode: "400001",
            Latitude: 19.07,
            Longitude: 72.87,
            ParkingType: ParkingType.Open,
            TotalSpots: 20,
            HourlyRate: 50m,
            DailyRate: 300m,
            WeeklyRate: 1500m,
            MonthlyRate: 5000m,
            OpenTime: null,
            CloseTime: null);

    private CorporateParkingSpaceDto SampleParkingSpace(Guid spaceId) =>
        new(
            Id: spaceId,
            CompanyId: _companyId,
            Title: "Lot A",
            Description: "Company lot",
            Address: "1 Main",
            City: "Mumbai",
            State: "MH",
            Country: "IN",
            PostalCode: "400001",
            Latitude: 19.07,
            Longitude: 72.87,
            ParkingType: ParkingType.Open,
            TotalSpots: 20,
            AvailableSpots: 18,
            HourlyRate: 50m,
            DailyRate: 300m,
            WeeklyRate: 1500m,
            MonthlyRate: 5000m,
            OpenTime: TimeSpan.Zero,
            CloseTime: TimeSpan.FromHours(23),
            Is24Hours: true,
            Amenities: new List<string>(),
            AllowedVehicleTypes: new List<VehicleType>(),
            ImageUrls: new List<string>(),
            IsActive: true,
            IsVerified: false,
            SpecialInstructions: null,
            ZoneCode: null,
            CreatedAt: DateTime.UtcNow);

    private ParkingAllocationDto SampleAllocation(Guid allocationId, Guid spaceId) =>
        new(
            Id: allocationId,
            CompanyId: _companyId,
            ParkingSpaceId: spaceId,
            ParkingSpaceTitle: "Lot A",
            TotalSlots: 10,
            FixedSlots: 2,
            SharedSlots: 8,
            MonthlyRate: 100m,
            StartDate: DateTime.UtcNow.Date,
            EndDate: DateTime.UtcNow.Date.AddMonths(6),
            Status: AllocationStatus.Active,
            SourceType: ParkingAllocationSource.CompanyOwned,
            VendorId: null,
            LeaseReference: null,
            ApprovedByUserId: _userId,
            ApprovedAt: DateTime.UtcNow,
            Policy: null,
            FixedAssignments: new List<FixedSlotAssignmentDto>(),
            CreatedAt: DateTime.UtcNow);

    private CorporateBookingDto SampleBooking(bool isVisitor) =>
        new(
            Id: Guid.NewGuid(),
            BookingId: Guid.NewGuid(),
            BookingReference: isVisitor ? "VIS-1" : "EMP-1",
            SlotType: CorporateSlotType.Shared,
            SlotNumber: 4,
            IsVisitorBooking: isVisitor,
            VisitorName: isVisitor ? "Guest" : null,
            VisitorLicensePlate: isVisitor ? "MH01XY9999" : null,
            StartDateTime: DateTime.UtcNow.AddHours(1),
            EndDateTime: DateTime.UtcNow.AddHours(3),
            BookingStatus: BookingStatus.Confirmed,
            QrCodeToken: "qr",
            CreatedAt: DateTime.UtcNow,
            AllocationId: Guid.NewGuid(),
            ParkingSpaceTitle: "Lot A",
            MembershipId: Guid.NewGuid(),
            MemberName: "Member",
            MemberEmail: "m@acme.com",
            TotalAmount: 100m,
            VehicleNumber: isVisitor ? null : "MH12AB1234");

    private static CompanyDashboardDto SampleDashboard() =>
        new(
            TotalMembers: 10,
            ActiveMembers: 8,
            TotalAllocations: 2,
            ActiveAllocations: 2,
            OwnedParkingSpaces: 1,
            OwnedParkingSlots: 20,
            LeasedAllocations: 1,
            PendingVendorAllocations: 0,
            TotalBookingsThisMonth: 15,
            VisitorBookingsThisMonth: 3,
            TotalHoursUsedThisMonth: 40m,
            MonthlySpend: 5000m,
            UtilizationPercentage: 55,
            BookingsByDay: new List<DashboardChartDataDto> { new("Mon", 100m, 5) },
            AllocationBreakdown: new List<AllocationUtilizationDto>
            {
                new(Guid.NewGuid(), "Lot A", 10, 4, 40)
            },
            ActiveWaitlistEntries: 1,
            SuspiciousActivityCount: 0,
            PeakHours: new List<PeakHourDto> { new(9, 3) },
            FraudAlerts: new List<FraudAlertDto>(),
            ExpiringAllocationsWithin30Days: 0,
            ExpiringAllocations: new List<ExpiringAllocationDto>());

    private CorporateInvoiceDetailDto SampleInvoice()
    {
        var invoiceId = Guid.NewGuid();
        return new CorporateInvoiceDetailDto(
            Id: invoiceId,
            InvoiceNumber: "INV-100",
            BillingTypeSnapshot: BillingType.UsageBased,
            PeriodStart: DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-30)),
            PeriodEnd: DateOnly.FromDateTime(DateTime.UtcNow.Date),
            Status: CorporateInvoiceStatus.Draft,
            Currency: "INR",
            Subtotal: 1000m,
            TaxAmount: 180m,
            TotalAmount: 1180m,
            GeneratedByUserId: _userId,
            CreatedAt: DateTime.UtcNow,
            IssuedAt: null,
            IssuedByUserId: null,
            PaidAt: null,
            PaidByUserId: null,
            PaymentReference: null,
            PaymentNotes: null,
            VoidedAt: null,
            VoidedByUserId: null,
            VoidReason: null,
            Lines: new List<CorporateInvoiceLineDto>
            {
                new(
                    Guid.NewGuid(),
                    CorporateInvoiceLineType.Usage,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Booking usage",
                    2m,
                    500m,
                    1000m)
            });
    }
}
