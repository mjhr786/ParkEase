using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate.Allocations;
using ParkingApp.Application.CQRS.Commands.Corporate.Bookings;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Marketplace.Contracts;

namespace ParkingApp.IntegrationTests.Support;

/// <summary>
/// Application-layer corporate fixture for dual-pool lifecycle integration tests.
/// Tracks company aggregates and class-scoped occupancy in memory (mirrors SQL filter semantics).
/// </summary>
internal sealed class InMemoryCorporateFixture
{
    private readonly Dictionary<Guid, Company> _companies = new();
    private readonly Dictionary<Guid, ParkingSpaceSummary> _spaces = new();
    /// <summary>allocationId → list of (vehicleClass, slotNumber) for active shared occupancy in a window.</summary>
    private readonly List<(Guid AllocationId, VehicleClass Class, int SlotNumber, Guid BookingId)> _sharedOccupancy = new();

    public Mock<ICorporateUnitOfWork> UnitOfWork { get; } = new();
    public Mock<ICompanyRepository> Companies { get; } = new();
    public Mock<ICorporateBookingRepository> Bookings { get; } = new();
    public Mock<IParkingSpaceLookup> Parking { get; } = new();
    public Mock<ICompanyQuotaCache> QuotaCache { get; } = new();
    public Mock<ICacheService> Cache { get; } = new();
    public Mock<IMarketplaceBookingService> Marketplace { get; } = new();

    public Guid AdminId { get; } = Guid.NewGuid();
    public Guid EmployeeAId { get; } = Guid.NewGuid();
    public Guid EmployeeBId { get; } = Guid.NewGuid();

    public InMemoryCorporateFixture()
    {
        UnitOfWork.Setup(u => u.Companies).Returns(Companies.Object);
        UnitOfWork.Setup(u => u.CorporateBookings).Returns(Bookings.Object);
        UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        Companies
            .Setup(r => r.GetWithAllocationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _companies.GetValueOrDefault(id));
        Companies
            .Setup(r => r.GetAggregateForBookingAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid companyId, Guid _, Guid _, DateTime __, DateTime ___, CancellationToken ____) =>
                _companies.GetValueOrDefault(companyId));

        Parking
            .Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                _spaces.TryGetValue(id, out var s) ? s : null);

        QuotaCache
            .Setup(q => q.GetAllocationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid companyId, Guid allocationId, CancellationToken _) =>
            {
                if (!_companies.TryGetValue(companyId, out var company))
                    return null;
                var allocation = company.Allocations.FirstOrDefault(a => a.Id == allocationId && !a.IsDeleted);
                if (allocation is null)
                    return null;
                return ToQuotaEntry(company, allocation);
            });
        QuotaCache
            .Setup(q => q.InvalidateCompanyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Cache.Setup(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Cache.Setup(c => c.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Cache.Setup(c => c.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Bookings
            .Setup(b => b.GetReservationPreCheckAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<string?>(), It.IsAny<VehicleClass>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                Guid _, Guid __, Guid allocationId,
                DateTime ___, DateTime ____,
                DateOnly _____, DateOnly ______,
                DateTime _______, DateTime ________,
                string? _________, VehicleClass vehicleClass, CancellationToken __________) =>
            {
                var occupied = _sharedOccupancy
                    .Where(o => o.AllocationId == allocationId && o.Class == vehicleClass)
                    .Select(o => o.SlotNumber)
                    .Distinct()
                    .ToList();
                return new CorporateReservationPreCheck
                {
                    DayBookingCount = 0,
                    WeekBookingCount = 0,
                    ActiveSharedBookingCount = occupied.Count,
                    OccupiedSharedSlotNumbers = occupied,
                    SharedSlotUsageBySlot = occupied.ToDictionary(s => s, _ => 1),
                    HasOverlappingMemberBooking = false,
                    HasOverlappingVehicleBooking = false,
                    RecentBookingCreateCount = 0
                };
            });

        Marketplace
            .Setup(m => m.StageCorporateBookingAsync(It.IsAny<StageCorporateBookingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StageCorporateBookingRequest req, CancellationToken _) =>
                new MarketplaceBookingCreateResult(Guid.NewGuid(), $"QR-{req.VehicleType}"));
    }

    public Company SeedCompany(string name = "Acme Dual")
    {
        var company = Company.Create(name, $"REG-{Guid.NewGuid():N}"[..12], "a@acme.com", "555", "Addr",
            BillingType.UsageBased, AdminId);
        company.AddMember(AdminId, EmployeeAId, CompanyRole.Employee);
        company.AddMember(AdminId, EmployeeBId, CompanyRole.Employee);
        _companies[company.Id] = company;
        return company;
    }

    public ParkingSpaceSummary SeedOwnedSpace(Guid companyId, int totalSpots = 30)
    {
        var space = new ParkingSpaceSummary(
            Guid.NewGuid(), Guid.NewGuid(), "HQ Lot", true, totalSpots, "CompanyOwned", companyId);
        _spaces[space.ParkingSpaceId] = space;
        return space;
    }

    public ParkingSpaceSummary SeedVendorSpace(int totalSpots = 30)
    {
        var ownerId = Guid.NewGuid();
        var space = new ParkingSpaceSummary(
            Guid.NewGuid(), ownerId, "Vendor Lot", true, totalSpots, "IndividualVendor");
        _spaces[space.ParkingSpaceId] = space;
        return space;
    }

    public static BookingPolicy OpenPolicy() =>
        BookingPolicy.Create(10, 40, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), allowWeekends: true);

    public static (DateTime Start, DateTime End) WeekdayWindow()
    {
        var start = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        return (start, start.AddHours(2));
    }

    /// <summary>Record shared occupancy after a successful book (domain does not expose slot number on corporate booking).</summary>
    public void TrackSharedOccupancy(Guid allocationId, VehicleClass vehicleClass, int slotNumber, Guid bookingId)
    {
        _sharedOccupancy.Add((allocationId, vehicleClass, slotNumber, bookingId));
    }

    public void ClearSharedOccupancy(Guid allocationId, VehicleClass vehicleClass)
    {
        _sharedOccupancy.RemoveAll(o => o.AllocationId == allocationId && o.Class == vehicleClass);
    }

    public CreateOwnedParkingAllocationHandler CreateOwnedHandler() =>
        new(UnitOfWork.Object, Parking.Object, QuotaCache.Object);

    public AllocateParkingSlotsHandler AllocateHandler() =>
        new(UnitOfWork.Object, Parking.Object, QuotaCache.Object);

    public AssignFixedSlotHandler AssignFixedHandler() =>
        new(UnitOfWork.Object, Parking.Object, QuotaCache.Object, Cache.Object);

    public BookCorporateParkingHandler BookEmployeeHandler() =>
        new(UnitOfWork.Object, Marketplace.Object, Cache.Object, QuotaCache.Object);

    public BookVisitorParkingHandler BookVisitorHandler() =>
        new(UnitOfWork.Object, Marketplace.Object, Cache.Object, QuotaCache.Object);

    private static CompanyQuotaCacheEntry ToQuotaEntry(Company company, ParkingAllocation allocation) =>
        new(
            company.Id, allocation.Id, allocation.ParkingSpaceId, "Lot", 50m, true,
            company.BillingType, allocation.Status, allocation.SourceType,
            allocation.VendorId, allocation.LeaseReference, allocation.ApprovedByUserId, allocation.ApprovedAt,
            allocation.Quota.TotalSlots, allocation.Quota.FixedSlots, allocation.Quota.SharedSlots,
            allocation.MonthlyRate, allocation.StartDate, allocation.EndDate, allocation.CreatedAt,
            allocation.BookingPolicy.MaxBookingsPerEmployeePerDay,
            allocation.BookingPolicy.MaxBookingsPerEmployeePerWeek,
            allocation.BookingPolicy.PriorityThreshold,
            allocation.BookingPolicy.AllowedStartTime,
            allocation.BookingPolicy.AllowedEndTime,
            allocation.BookingPolicy.AllowWeekends,
            TwoWheeler: new ClassPoolSnapshot(
                allocation.TwoWheelerQuota.TotalSlots,
                allocation.TwoWheelerQuota.FixedSlots,
                allocation.TwoWheelerQuota.SharedSlots),
            FourWheeler: new ClassPoolSnapshot(
                allocation.FourWheelerQuota.TotalSlots,
                allocation.FourWheelerQuota.FixedSlots,
                allocation.FourWheelerQuota.SharedSlots));
}
