using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Queries.Corporate;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Corporate;

public class CompanyQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
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

        var handler = new GetCompanyDashboardHandler(_uow.Object, _readStore.Object);
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

        var handler = new GetCompanyDashboardHandler(_uow.Object, _readStore.Object);
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
}
