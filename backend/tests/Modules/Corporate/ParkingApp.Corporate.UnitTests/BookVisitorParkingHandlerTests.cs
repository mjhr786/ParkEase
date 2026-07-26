using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Bookings;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>Wave 16: BookVisitorParkingHandler paths in the Corporate module suite.</summary>
public class BookVisitorParkingHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _corporate = new();
    private readonly Mock<IMarketplaceBookingService> _marketplace = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<ICompanyQuotaCache> _quotaCache = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICorporateBookingRepository> _bookings = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _employeeId = Guid.NewGuid();

    public BookVisitorParkingHandlerTests()
    {
        _corporate.Setup(x => x.Companies).Returns(_companies.Object);
        _corporate.Setup(x => x.CorporateBookings).Returns(_bookings.Object);
        _corporate.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _cache.Setup(x => x.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _cache.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _quotaCache.Setup(x => x.InvalidateCompanyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private static (DateTime Start, DateTime End) WeekdayWindow()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        return (start, start.AddHours(2));
    }

    private static CompanyQuotaCacheEntry BookableQuota(
        Company company, ParkingAllocation allocation, Guid spaceId, decimal hourlyRate = 50m) =>
        new(
            company.Id, allocation.Id, spaceId, "Lot", hourlyRate, true,
            company.BillingType, AllocationStatus.Active, ParkingAllocationSource.CompanyOwned,
            null, null, null, null, 5, 0, 5, 0m, allocation.StartDate, allocation.EndDate, DateTime.UtcNow,
            5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true);

    [Fact]
    public async Task BookVisitor_WhenQuotaMissing_ReturnsNotFound()
    {
        _quotaCache.Setup(x => x.GetAllocationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyQuotaCacheEntry?)null);

        var handler = new BookVisitorParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var (start, end) = WeekdayWindow();
        var result = await handler.HandleAsync(new BookVisitorParkingCommand(
            Guid.NewGuid(), _employeeId,
            new BookVisitorParkingDto(Guid.NewGuid(), start, end, "Guest", "KA01AB1", end.AddHours(1))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Allocation not found");
    }

    [Fact]
    public async Task BookVisitor_WhenNotBookable_ReturnsFailure()
    {
        var companyId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        _quotaCache.Setup(x => x.GetAllocationAsync(companyId, allocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyQuotaCacheEntry(
                companyId, allocationId, Guid.NewGuid(), "Lot", 50m, false,
                BillingType.UsageBased, AllocationStatus.PendingApproval, ParkingAllocationSource.CompanyOwned,
                null, null, null, null, 5, 0, 5, 0m, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), DateTime.UtcNow,
                1, 5, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true));

        var handler = new BookVisitorParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var (start, end) = WeekdayWindow();
        var result = await handler.HandleAsync(new BookVisitorParkingCommand(
            companyId, _employeeId,
            new BookVisitorParkingDto(allocationId, start, end, "Guest", "KA01AB1", end.AddHours(1))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Active allocation not found");
    }

    [Fact]
    public async Task BookVisitor_WhenCompanyMissing_ReturnsNotFound()
    {
        var companyId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var (start, end) = WeekdayWindow();

        _quotaCache.Setup(x => x.GetAllocationAsync(companyId, allocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyQuotaCacheEntry(
                companyId, allocationId, spaceId, "Lot", 50m, true,
                BillingType.UsageBased, AllocationStatus.Active, ParkingAllocationSource.CompanyOwned,
                null, null, null, null, 5, 0, 5, 0m,
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                DateTime.UtcNow,
                5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                companyId, _employeeId, allocationId, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var handler = new BookVisitorParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookVisitorParkingCommand(
            companyId, _employeeId,
            new BookVisitorParkingDto(allocationId, start, end, "Guest", "KA01AB1", end.AddHours(1))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Company not found");
    }

    [Fact]
    public async Task BookVisitor_WhenLockNotAcquired_ReturnsBusy()
    {
        var company = Company.Create("Acme", "REG-V1", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        company.AddMember(_adminId, _employeeId, CompanyRole.Employee);
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), allowWeekends: true);
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, Guid.NewGuid(), Quota.Create(5, 0, 5), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 5, bookingPolicy: policy);

        var (start, end) = WeekdayWindow();
        _quotaCache.Setup(x => x.GetAllocationAsync(company.Id, allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookableQuota(company, allocation, allocation.ParkingSpaceId));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                company.Id, _employeeId, allocation.Id, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _cache.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new BookVisitorParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookVisitorParkingCommand(
            company.Id, _employeeId,
            new BookVisitorParkingDto(allocation.Id, start, end, "Guest", "KA01AB1", end.AddHours(1))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("processing other bookings");
    }

    [Fact]
    public async Task BookVisitor_WhenValid_BooksSuccessfully()
    {
        var company = Company.Create("Acme", "REG-V2", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        company.AddMember(_adminId, _employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), allowWeekends: true);
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId, Quota.Create(5, 0, 5), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 5, bookingPolicy: policy);

        var (start, end) = WeekdayWindow();
        var bookingId = Guid.NewGuid();

        _quotaCache.Setup(x => x.GetAllocationAsync(company.Id, allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookableQuota(company, allocation, spaceId));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                company.Id, _employeeId, allocation.Id, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _bookings.Setup(x => x.GetReservationPreCheckAsync(
                company.Id, It.IsAny<Guid>(), allocation.Id, start, end,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CorporateReservationPreCheck
            {
                DayBookingCount = 0,
                WeekBookingCount = 0,
                ActiveSharedBookingCount = 0,
                OccupiedSharedSlotNumbers = Array.Empty<int>(),
                SharedSlotUsageBySlot = new Dictionary<int, int>(),
                HasOverlappingMemberBooking = false,
                HasOverlappingVehicleBooking = false,
                RecentBookingCreateCount = 0
            });
        _marketplace.Setup(x => x.StageCorporateBookingAsync(It.IsAny<StageCorporateBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketplaceBookingCreateResult(bookingId, "QR-VIS"));

        var handler = new BookVisitorParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookVisitorParkingCommand(
            company.Id, _employeeId,
            new BookVisitorParkingDto(allocation.Id, start, end, "Visitor Guest", "KA09ZZ9999", end.AddHours(1))));

        result.Success.Should().BeTrue(result.Message);
        result.Message.Should().Contain("Visitor parking booked");
        result.Data!.Booking.Should().NotBeNull();
        result.Data.Booking!.IsVisitorBooking.Should().BeTrue();
        result.Data.Waitlist.Should().BeNull();
        _corporate.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _quotaCache.Verify(x => x.InvalidateCompanyAsync(company.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookVisitor_WhenSharedFull_Waitlists()
    {
        var company = Company.Create("Acme", "REG-V3", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        company.AddMember(_adminId, _employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), allowWeekends: true);
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId, Quota.Create(1, 0, 1), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 1, bookingPolicy: policy);

        var (start, end) = WeekdayWindow();
        var bookingId = Guid.NewGuid();

        _quotaCache.Setup(x => x.GetAllocationAsync(company.Id, allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookableQuota(company, allocation, spaceId));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                company.Id, _employeeId, allocation.Id, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _bookings.Setup(x => x.GetReservationPreCheckAsync(
                company.Id, It.IsAny<Guid>(), allocation.Id, start, end,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CorporateReservationPreCheck
            {
                DayBookingCount = 0,
                WeekBookingCount = 0,
                ActiveSharedBookingCount = 1,
                OccupiedSharedSlotNumbers = new[] { 1 },
                SharedSlotUsageBySlot = new Dictionary<int, int> { [1] = 1 },
                HasOverlappingMemberBooking = false,
                HasOverlappingVehicleBooking = false,
                RecentBookingCreateCount = 0
            });
        _marketplace.Setup(x => x.StageCorporateBookingAsync(It.IsAny<StageCorporateBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketplaceBookingCreateResult(bookingId, "QR-WL"));

        var handler = new BookVisitorParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookVisitorParkingCommand(
            company.Id, _employeeId,
            new BookVisitorParkingDto(allocation.Id, start, end, "Wait Guest", "MH12AB1234", end.AddHours(1))));

        result.Success.Should().BeTrue(result.Message);
        result.Message.Should().Contain("waitlist");
        result.Data!.Waitlist.Should().NotBeNull();
        result.Data.Booking.Should().BeNull();
    }
}
