using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Bookings;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.Corporate.UnitTests;

public class BookCorporateParkingHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _corporate = new();
    private readonly Mock<IMarketplaceBookingService> _marketplace = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<ICompanyQuotaCache> _quotaCache = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICorporateBookingRepository> _bookings = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _employeeId = Guid.NewGuid();

    public BookCorporateParkingHandlerTests()
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

    [Fact]
    public async Task Book_WhenQuotaMissing_ReturnsNotFound()
    {
        _quotaCache.Setup(x => x.GetAllocationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyQuotaCacheEntry?)null);

        var handler = new BookCorporateParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookCorporateParkingCommand(
            Guid.NewGuid(), _employeeId,
            new BookCorporateParkingDto(Guid.NewGuid(), DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Allocation not found");
    }

    [Fact]
    public async Task Book_WhenLockNotAcquired_ReturnsBusy()
    {
        var company = Company.Create("Acme", "REG-B1", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        company.AddMember(_adminId, _employeeId, CompanyRole.Employee);
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, Guid.NewGuid(), Quota.Create(5, 0, 5), 0m,
            DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), parkingCapacity: 5);

        _quotaCache.Setup(x => x.GetAllocationAsync(company.Id, allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyQuotaCacheEntry(
                company.Id, allocation.Id, allocation.ParkingSpaceId, "Lot", 0m, true,
                company.BillingType, AllocationStatus.Active, ParkingAllocationSource.CompanyOwned,
                null, null, null, null, 5, 0, 5, 0m, allocation.StartDate, allocation.EndDate, DateTime.UtcNow,
                1, 5, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                company.Id, _employeeId, allocation.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _cache.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new BookCorporateParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookCorporateParkingCommand(
            company.Id, _employeeId,
            new BookCorporateParkingDto(allocation.Id, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), VehicleType.Car, "KA01AB1")));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("processing other bookings");
    }

    [Fact]
    public async Task Book_WhenNotBookable_ReturnsFailure()
    {
        var companyId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        _quotaCache.Setup(x => x.GetAllocationAsync(companyId, allocationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyQuotaCacheEntry(
                companyId, allocationId, Guid.NewGuid(), "Lot", 50m, false,
                BillingType.UsageBased, AllocationStatus.PendingApproval, ParkingAllocationSource.CompanyOwned,
                null, null, null, null, 5, 0, 5, 0m, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1), DateTime.UtcNow,
                1, 5, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true));

        var handler = new BookCorporateParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookCorporateParkingCommand(
            companyId, _employeeId,
            new BookCorporateParkingDto(allocationId, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Active allocation not found");
    }

    /// <summary>Wednesday 10:00–12:00 UTC — within default 07:00–22:00 policy and not weekend.</summary>
    private static (DateTime Start, DateTime End) WeekdayWindow()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc); // Wednesday
        return (start, start.AddHours(2));
    }

    [Fact]
    public async Task Book_WhenValid_BooksSuccessfully()
    {
        var company = Company.Create("Acme", "REG-B2", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        company.AddMember(_adminId, _employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), allowWeekends: true);
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId, Quota.Create(5, 0, 5), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 5,
            bookingPolicy: policy);

        var (start, end) = WeekdayWindow();
        var bookingId = Guid.NewGuid();

        _quotaCache.Setup(x => x.GetAllocationAsync(company.Id, allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyQuotaCacheEntry(
                company.Id, allocation.Id, spaceId, "Lot", 50m, true,
                company.BillingType, AllocationStatus.Active, ParkingAllocationSource.CompanyOwned,
                null, null, null, null, 5, 0, 5, 0m, allocation.StartDate, allocation.EndDate, DateTime.UtcNow,
                5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                company.Id, _employeeId, allocation.Id, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _bookings.Setup(x => x.GetReservationPreCheckAsync(
                company.Id, It.IsAny<Guid>(), allocation.Id, start, end,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), It.IsAny<ParkingApp.BuildingBlocks.Enums.VehicleClass>(), It.IsAny<CancellationToken>()))
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
            .ReturnsAsync(new MarketplaceBookingCreateResult(bookingId, "QR-TOKEN"));

        var handler = new BookCorporateParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookCorporateParkingCommand(
            company.Id, _employeeId,
            new BookCorporateParkingDto(allocation.Id, start, end, VehicleType.Car, "KA01AB1234")));

        result.Success.Should().BeTrue(result.Message);
        result.Message.Should().Contain("booked successfully");
        result.Data!.Booking.Should().NotBeNull();
        result.Data.Booking!.BookingId.Should().Be(bookingId);
        result.Data.Waitlist.Should().BeNull();
        _corporate.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _quotaCache.Verify(x => x.InvalidateCompanyAsync(company.Id, It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(x => x.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Book_WhenFraudBlocked_ReturnsFailure()
    {
        var company = Company.Create("Acme", "REG-B3", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        company.AddMember(_adminId, _employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var policy = BookingPolicy.Create(5, 20, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), allowWeekends: true);
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId, Quota.Create(5, 0, 5), 0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 5,
            bookingPolicy: policy);

        var (start, end) = WeekdayWindow();

        _quotaCache.Setup(x => x.GetAllocationAsync(company.Id, allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyQuotaCacheEntry(
                company.Id, allocation.Id, spaceId, "Lot", 50m, true,
                company.BillingType, AllocationStatus.Active, ParkingAllocationSource.CompanyOwned,
                null, null, null, null, 5, 0, 5, 0m, allocation.StartDate, allocation.EndDate, DateTime.UtcNow,
                5, 20, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), true));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                company.Id, _employeeId, allocation.Id, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _bookings.Setup(x => x.GetReservationPreCheckAsync(
                company.Id, It.IsAny<Guid>(), allocation.Id, start, end,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), It.IsAny<ParkingApp.BuildingBlocks.Enums.VehicleClass>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CorporateReservationPreCheck
            {
                DayBookingCount = 0,
                WeekBookingCount = 0,
                ActiveSharedBookingCount = 0,
                OccupiedSharedSlotNumbers = Array.Empty<int>(),
                SharedSlotUsageBySlot = new Dictionary<int, int>(),
                HasOverlappingMemberBooking = true,
                HasOverlappingVehicleBooking = false,
                RecentBookingCreateCount = 0
            });
        _marketplace.Setup(x => x.StageCorporateBookingAsync(It.IsAny<StageCorporateBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketplaceBookingCreateResult(Guid.NewGuid(), null));

        var handler = new BookCorporateParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookCorporateParkingCommand(
            company.Id, _employeeId,
            new BookCorporateParkingDto(allocation.Id, start, end, VehicleType.Car, "KA01AB1234")));

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
        _corporate.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static BookingPolicy OpenPolicy() =>
        BookingPolicy.Create(10, 40, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), allowWeekends: true);

    private static CompanyQuotaCacheEntry DualQuota(
        Company company, ParkingAllocation allocation, Guid spaceId) =>
        new(
            company.Id, allocation.Id, spaceId, "Lot", 50m, true,
            company.BillingType, AllocationStatus.Active, ParkingAllocationSource.CompanyOwned,
            null, null, null, null,
            allocation.Quota.TotalSlots, allocation.Quota.FixedSlots, allocation.Quota.SharedSlots,
            0m, allocation.StartDate, allocation.EndDate, DateTime.UtcNow,
            10, 40, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), true,
            TwoWheeler: new ClassPoolSnapshot(
                allocation.TwoWheelerQuota.TotalSlots,
                allocation.TwoWheelerQuota.FixedSlots,
                allocation.TwoWheelerQuota.SharedSlots),
            FourWheeler: new ClassPoolSnapshot(
                allocation.FourWheelerQuota.TotalSlots,
                allocation.FourWheelerQuota.FixedSlots,
                allocation.FourWheelerQuota.SharedSlots));

    private static CorporateReservationPreCheck EmptyPreCheck(params int[] occupied) =>
        new()
        {
            DayBookingCount = 0,
            WeekBookingCount = 0,
            ActiveSharedBookingCount = occupied.Length,
            OccupiedSharedSlotNumbers = occupied,
            SharedSlotUsageBySlot = occupied.ToDictionary(s => s, _ => 1),
            HasOverlappingMemberBooking = false,
            HasOverlappingVehicleBooking = false,
            RecentBookingCreateCount = 0
        };

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public async Task Book_Motorcycle_WhenTwoWheelerSharedFull_DoesNotUseFourWheeler()
    {
        var company = Company.Create("Acme", "REG-B-2W", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        company.AddMember(_adminId, _employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId,
            Quota.CreatePool(1, 0, 1),
            Quota.CreatePool(5, 0, 5),
            0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());

        var (start, end) = WeekdayWindow();
        _quotaCache.Setup(x => x.GetAllocationAsync(company.Id, allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DualQuota(company, allocation, spaceId));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                company.Id, _employeeId, allocation.Id, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _bookings.Setup(x => x.GetReservationPreCheckAsync(
                company.Id, It.IsAny<Guid>(), allocation.Id, start, end,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), VehicleClass.TwoWheeler, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPreCheck(1));
        _marketplace.Setup(x => x.StageCorporateBookingAsync(It.IsAny<StageCorporateBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketplaceBookingCreateResult(Guid.NewGuid(), null));

        var handler = new BookCorporateParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookCorporateParkingCommand(
            company.Id, _employeeId,
            new BookCorporateParkingDto(allocation.Id, start, end, VehicleType.Motorcycle, "KA01BIKE")));

        result.Success.Should().BeTrue(result.Message);
        result.Message.Should().Contain("waitlist");
        result.Data!.Waitlist.Should().NotBeNull();
        result.Data.Booking.Should().BeNull();
        company.CorporateBookings.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public async Task Book_Car_WhenFourWheelerSharedFull_DoesNotUseTwoWheeler()
    {
        var company = Company.Create("Acme", "REG-B-4W", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        company.AddMember(_adminId, _employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId,
            Quota.CreatePool(5, 0, 5),
            Quota.CreatePool(1, 0, 1),
            0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());

        var (start, end) = WeekdayWindow();
        _quotaCache.Setup(x => x.GetAllocationAsync(company.Id, allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DualQuota(company, allocation, spaceId));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                company.Id, _employeeId, allocation.Id, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _bookings.Setup(x => x.GetReservationPreCheckAsync(
                company.Id, It.IsAny<Guid>(), allocation.Id, start, end,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), VehicleClass.FourWheeler, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPreCheck(1));
        _marketplace.Setup(x => x.StageCorporateBookingAsync(It.IsAny<StageCorporateBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketplaceBookingCreateResult(Guid.NewGuid(), null));

        var handler = new BookCorporateParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookCorporateParkingCommand(
            company.Id, _employeeId,
            new BookCorporateParkingDto(allocation.Id, start, end, VehicleType.Car, "KA01CAR1")));

        result.Success.Should().BeTrue(result.Message);
        result.Message.Should().Contain("waitlist");
        result.Data!.Waitlist.Should().NotBeNull();
        result.Data.Booking.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public async Task Book_Motorcycle_WhenTwoWheelerPoolZero_Fails()
    {
        var company = Company.Create("Acme", "REG-B-0-2W", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        company.AddMember(_adminId, _employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId,
            Quota.None,
            Quota.CreatePool(5, 0, 5),
            0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 5,
            bookingPolicy: OpenPolicy());

        var (start, end) = WeekdayWindow();
        _quotaCache.Setup(x => x.GetAllocationAsync(company.Id, allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DualQuota(company, allocation, spaceId));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                company.Id, _employeeId, allocation.Id, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _bookings.Setup(x => x.GetReservationPreCheckAsync(
                company.Id, It.IsAny<Guid>(), allocation.Id, start, end,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), VehicleClass.TwoWheeler, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPreCheck());
        _marketplace.Setup(x => x.StageCorporateBookingAsync(It.IsAny<StageCorporateBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketplaceBookingCreateResult(Guid.NewGuid(), null));

        var handler = new BookCorporateParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookCorporateParkingCommand(
            company.Id, _employeeId,
            new BookCorporateParkingDto(allocation.Id, start, end, VehicleType.Motorcycle, "KA01BIKE")));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("2-wheeler");
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public async Task Book_Motorcycle_WithTwoWheelerFixedAssignment_UsesFixed()
    {
        var company = Company.Create("Acme", "REG-B-FX2W", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var membership = company.AddMember(_adminId, _employeeId, CompanyRole.Employee);
        var spaceId = Guid.NewGuid();
        var allocation = company.CreateOwnedParkingAllocation(
            _adminId, spaceId,
            Quota.CreatePool(2, 1, 1),
            Quota.CreatePool(2, 0, 2),
            0m,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            parkingCapacity: 10,
            bookingPolicy: OpenPolicy());
        company.AssignFixedSlot(_adminId, allocation.Id, membership.Id, VehicleClass.TwoWheeler, 1);

        var (start, end) = WeekdayWindow();
        var bookingId = Guid.NewGuid();
        _quotaCache.Setup(x => x.GetAllocationAsync(company.Id, allocation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DualQuota(company, allocation, spaceId));
        _companies.Setup(x => x.GetAggregateForBookingAsync(
                company.Id, _employeeId, allocation.Id, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _bookings.Setup(x => x.GetReservationPreCheckAsync(
                company.Id, It.IsAny<Guid>(), allocation.Id, start, end,
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), VehicleClass.TwoWheeler, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyPreCheck(2)); // shared full; fixed should still work
        _marketplace.Setup(x => x.StageCorporateBookingAsync(It.IsAny<StageCorporateBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketplaceBookingCreateResult(bookingId, "QR"));

        var handler = new BookCorporateParkingHandler(
            _corporate.Object, _marketplace.Object, _cache.Object, _quotaCache.Object);

        var result = await handler.HandleAsync(new BookCorporateParkingCommand(
            company.Id, _employeeId,
            new BookCorporateParkingDto(allocation.Id, start, end, VehicleType.Motorcycle, "KA01BIKE")));

        result.Success.Should().BeTrue(result.Message);
        result.Data!.Booking.Should().NotBeNull();
        result.Data.Booking!.BookingId.Should().Be(bookingId);
        result.Data.Waitlist.Should().BeNull();
    }
}

