using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Queries.Corporate;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Corporate.Domain.Interfaces;
using Xunit;

namespace ParkingApp.Corporate.UnitTests;

public class CompanyQueryHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICompanyReadStore> _readStore = new();

    public CompanyQueryHandlerTests()
    {
        _uow.Setup(u => u.Companies).Returns(_companies.Object);
    }

    [Fact]
    public async Task GetMyCompanies_ReturnsStoreResults()
    {
        var userId = Guid.NewGuid();
        var list = new List<CompanyDto>
        {
            new(Guid.NewGuid(), "Acme", "REG", "a@b.com", "1", "addr", BillingType.ReservedSlots, true, 1, 0, DateTime.UtcNow)
        };
        _readStore.Setup(r => r.GetMyCompaniesAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var handler = new GetMyCompaniesHandler(_readStore.Object);
        var result = await handler.HandleAsync(new GetMyCompaniesQuery(userId));

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].Name.Should().Be("Acme");
    }

    [Fact]
    public async Task GetCompanyDetails_WhenNotMember_Denies()
    {
        _companies.Setup(c => c.GetMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCompanyMembership?)null);

        var handler = new GetCompanyDetailsHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyDetailsQuery(Guid.NewGuid(), Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Access denied");
        _readStore.Verify(r => r.GetCompanyDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCompanyDetails_WhenMember_ReturnsDto()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membership = UserCompanyMembership.Create(companyId, userId, CompanyRole.Employee);
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var dto = new CompanyDto(companyId, "Acme", "REG", "a@b.com", "1", "addr", BillingType.ReservedSlots, true, 3, 1, DateTime.UtcNow);
        _readStore.Setup(r => r.GetCompanyDetailsAsync(companyId, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var handler = new GetCompanyDetailsHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyDetailsQuery(companyId, userId));

        result.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("Acme");
    }

    [Fact]
    public async Task GetCompanyDashboard_WhenNotAdmin_Denies()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membership = UserCompanyMembership.Create(companyId, userId, CompanyRole.Employee);
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = new GetCompanyDashboardHandler(_uow.Object, _readStore.Object, new Mock<ParkingApp.Application.Interfaces.ICacheService>().Object);
        var result = await handler.HandleAsync(new GetCompanyDashboardQuery(companyId, userId));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Only company admins");
        _readStore.Verify(
            r => r.GetCompanyDashboardAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCompanyDashboard_WhenAdmin_ReturnsDashboard()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membership = UserCompanyMembership.Create(companyId, userId, CompanyRole.Admin);
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var dashboard = new CompanyDashboardDto(
            10, 8, 2, 1, 0, 0, 1, 0, 5, 1, 12.5m, 1000m, 40.0,
            new List<DashboardChartDataDto>(), new List<AllocationUtilizationDto>(),
            0, 0, new List<PeakHourDto>(), new List<FraudAlertDto>());
        _readStore.Setup(r => r.GetCompanyDashboardAsync(companyId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        var handler = new GetCompanyDashboardHandler(_uow.Object, _readStore.Object, new Mock<ParkingApp.Application.Interfaces.ICacheService>().Object);
        var result = await handler.HandleAsync(new GetCompanyDashboardQuery(companyId, userId));

        result.Success.Should().BeTrue();
        result.Data!.TotalMembers.Should().Be(10);
    }

    [Fact]
    public async Task GetVendorAllocations_DelegatesToReadStore()
    {
        var vendorId = Guid.NewGuid();
        _readStore.Setup(r => r.GetVendorAllocationsAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<VendorParkingAllocationDto>());

        var handler = new GetVendorAllocationsHandler(_readStore.Object);
        var result = await handler.HandleAsync(new GetVendorAllocationsQuery(vendorId));

        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemberBookings_WhenEmployee_RequestsOnlyOwnBookings()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membership = UserCompanyMembership.Create(companyId, userId, CompanyRole.Employee);
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        _readStore.Setup(r => r.GetMemberBookingsAsync(
                companyId, membership.Id, true, 0, 20, It.IsAny<CorporateBookingListFilter?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<CorporateBookingDto>(), 0));

        var handler = new GetMemberBookingsHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetMemberBookingsQuery(companyId, userId, 1, 20));

        result.Success.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(0);
        _readStore.Verify(r => r.GetMemberBookingsAsync(
            companyId, membership.Id, true, 0, 20, It.IsAny<CorporateBookingListFilter?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMemberBookings_WhenAdmin_RequestsCompanyWideBookings()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membership = UserCompanyMembership.Create(companyId, userId, CompanyRole.Admin);
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var booking = new CorporateBookingDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "CORP-1",
            CorporateSlotType.Shared,
            3,
            false,
            null,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            BookingStatus.Confirmed,
            "QR",
            DateTime.UtcNow,
            Guid.NewGuid(),
            "Lot A",
            membership.Id,
            "Ada Admin",
            "ada@acme.com");

        _readStore.Setup(r => r.GetMemberBookingsAsync(
                companyId, membership.Id, false, 0, 20, It.IsAny<CorporateBookingListFilter?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CorporateBookingDto> { booking }, 1));

        var handler = new GetMemberBookingsHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetMemberBookingsQuery(companyId, userId, 1, 20));

        result.Success.Should().BeTrue();
        result.Data!.Bookings.Should().HaveCount(1);
        result.Data.Bookings[0].ParkingSpaceTitle.Should().Be("Lot A");
        _readStore.Verify(r => r.GetMemberBookingsAsync(
            companyId, membership.Id, false, 0, 20, It.IsAny<CorporateBookingListFilter?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMemberBookings_PassesStatusAndDateFilters()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membership = UserCompanyMembership.Create(companyId, userId, CompanyRole.Admin);
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        CorporateBookingListFilter? captured = null;
        _readStore.Setup(r => r.GetMemberBookingsAsync(
                companyId, membership.Id, false, 0, 20, It.IsAny<CorporateBookingListFilter?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, bool, int, int, CorporateBookingListFilter?, CancellationToken>(
                (_, _, _, _, _, filter, _) => captured = filter)
            .ReturnsAsync((Array.Empty<CorporateBookingDto>(), 0));

        var from = DateTime.UtcNow.Date;
        var to = from.AddDays(7);
        var handler = new GetMemberBookingsHandler(_uow.Object, _readStore.Object);
        await handler.HandleAsync(new GetMemberBookingsQuery(
            companyId, userId, 1, 20, BookingStatus.Confirmed, false, from, to));

        captured.Should().NotBeNull();
        captured!.Status.Should().Be(BookingStatus.Confirmed);
        captured.IsVisitor.Should().BeFalse();
        captured.FromUtc.Should().Be(from);
        captured.ToUtc.Should().Be(to);
    }

    // ── Wave 16: remaining company list queries ─────────────────────────────

    [Fact]
    public async Task GetCompanyMembers_WhenNotAdmin_Denies()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, userId, CompanyRole.Employee));

        var handler = new GetCompanyMembersHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyMembersQuery(companyId, userId));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("admins");
    }

    [Fact]
    public async Task GetCompanyMembers_WhenAdmin_ReturnsPage()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, userId, CompanyRole.Admin));

        var members = new List<MembershipDto>
        {
            new(Guid.NewGuid(), userId, "Admin", "a@acme.com", CompanyRole.Admin, null, 1, true, DateTime.UtcNow, companyId)
        };
        _readStore.Setup(r => r.GetCompanyMembersAsync(companyId, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((members, 1));

        var handler = new GetCompanyMembersHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyMembersQuery(companyId, userId, 1, 50));

        result.Success.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Members.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCompanyInvitations_WhenAdmin_MapsTokensAndExpired()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, userId, CompanyRole.Admin));

        var pending = EmployeeInvitation.Create(companyId, "p@acme.com", CompanyRole.Employee, userId, expiresInDays: 7);
        var expired = EmployeeInvitation.Create(companyId, "e@acme.com", CompanyRole.Employee, userId, expiresInDays: 1);
        // Force past expiry via MarkExpired then leave as Expired without pending token exposure
        expired.MarkExpired();

        var invites = new Mock<IEmployeeInvitationRepository>();
        invites.Setup(i => i.GetByCompanyIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmployeeInvitation> { pending, expired });
        _uow.Setup(u => u.EmployeeInvitations).Returns(invites.Object);

        var handler = new GetCompanyInvitationsHandler(_uow.Object);
        var result = await handler.HandleAsync(new GetCompanyInvitationsQuery(companyId, userId));

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data!.Single(d => d.Email == "p@acme.com").InvitationToken.Should().NotBeNullOrWhiteSpace();
        result.Data.Single(d => d.Email == "e@acme.com").InvitationToken.Should().BeNull();
        result.Data.Single(d => d.Email == "e@acme.com").Status.Should().Be(InvitationStatus.Expired);
    }

    [Fact]
    public async Task GetCompanyInvitations_WhenNotAdmin_Denies()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, userId, CompanyRole.Employee));

        var handler = new GetCompanyInvitationsHandler(_uow.Object);
        var result = await handler.HandleAsync(new GetCompanyInvitationsQuery(companyId, userId));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("admins");
    }

    [Fact]
    public async Task GetCompanyAllocations_WhenMember_MapsQuotaCache()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, userId, CompanyRole.Employee));

        var quotaCache = new Mock<ICompanyQuotaCache>();
        quotaCache.Setup(q => q.GetCompanyAllocationsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompanyQuotaCacheEntry>
            {
                new(
                    companyId, allocationId, spaceId, "Lot A", 25m, true,
                    BillingType.UsageBased, AllocationStatus.Active, ParkingAllocationSource.CompanyOwned,
                    null, null, null, null, 10, 2, 8, 0m,
                    DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(3), DateTime.UtcNow,
                    2, 10, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true)
            });

        _readStore.Setup(r => r.GetFixedAssignmentsByAllocationAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, List<FixedSlotAssignmentDto>>
            {
                [allocationId] = new List<FixedSlotAssignmentDto>
                {
                    new(Guid.NewGuid(), "Ada", 1, DateTime.UtcNow)
                }
            });

        var handler = new GetCompanyAllocationsHandler(_uow.Object, quotaCache.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyAllocationsQuery(companyId, userId));

        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle();
        result.Data![0].ParkingSpaceTitle.Should().Be("Lot A");
        result.Data[0].FixedAssignments.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCompanyAllocations_WhenNotMember_Denies()
    {
        _companies.Setup(c => c.GetMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCompanyMembership?)null);

        var handler = new GetCompanyAllocationsHandler(
            _uow.Object, new Mock<ICompanyQuotaCache>().Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyAllocationsQuery(Guid.NewGuid(), Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Access denied");
    }

    [Fact]
    public async Task GetCompanyParkingSpaces_WhenAdmin_ReturnsSpaces()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, userId, CompanyRole.Admin));
        _readStore.Setup(r => r.GetCompanyOwnedParkingSpacesAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CorporateParkingSpaceDto>());

        var handler = new GetCompanyParkingSpacesHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyParkingSpacesQuery(companyId, userId));

        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCompanyParkingSpaces_WhenNotAdmin_Denies()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, userId, CompanyRole.Employee));

        var handler = new GetCompanyParkingSpacesHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyParkingSpacesQuery(companyId, userId));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("admins");
    }

    [Fact]
    public async Task GetCompanyWaitlist_WhenMember_ReturnsEntries()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membership = UserCompanyMembership.Create(companyId, userId, CompanyRole.Employee);
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var entry = new CorporateWaitlistDto(
            Guid.NewGuid(), Guid.NewGuid(), false,
            DateTime.UtcNow, DateTime.UtcNow.AddHours(2), "KA01", null, null,
            WaitlistStatus.Pending, 1, 1, DateTime.UtcNow);
        _readStore.Setup(r => r.GetCompanyWaitlistAsync(
                companyId, membership.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CorporateWaitlistDto> { entry });

        var handler = new GetCompanyWaitlistHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyWaitlistQuery(companyId, userId));

        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle();
        _readStore.Verify(r => r.GetCompanyWaitlistAsync(
            companyId, membership.Id, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCompanyInvoices_WhenAdmin_ReturnsList()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, userId, CompanyRole.Admin));

        var items = new List<CorporateInvoiceSummaryDto>
        {
            new(
                Guid.NewGuid(), "INV-1", BillingType.UsageBased,
                DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
                DateOnly.FromDateTime(DateTime.UtcNow),
                CorporateInvoiceStatus.Issued, "INR", 100m, 18m, 118m, 2,
                DateTime.UtcNow, DateTime.UtcNow, null, null)
        };
        _readStore.Setup(r => r.GetCompanyInvoicesAsync(
                companyId, null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        var handler = new GetCompanyInvoicesHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyInvoicesQuery(companyId, userId));

        result.Success.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items[0].InvoiceNumber.Should().Be("INV-1");
    }

    [Fact]
    public async Task GetCompanyInvoices_WhenNotAdmin_Denies()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, userId, CompanyRole.Employee));

        var handler = new GetCompanyInvoicesHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCompanyInvoicesQuery(companyId, userId));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("admins");
    }

    [Fact]
    public async Task GetCorporateInvoiceDetails_WhenMissing_ReturnsNotFound()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        _companies.Setup(c => c.GetMembershipAsync(companyId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, userId, CompanyRole.Admin));
        _readStore.Setup(r => r.GetCorporateInvoiceDetailAsync(companyId, invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CorporateInvoiceDetailDto?)null);

        var handler = new GetCorporateInvoiceDetailsHandler(_uow.Object, _readStore.Object);
        var result = await handler.HandleAsync(new GetCorporateInvoiceDetailsQuery(companyId, userId, invoiceId));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }
}

