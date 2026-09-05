using ParkingApp.Application.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Corporate.Application.Services;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Corporate.Domain.Interfaces;
using Xunit;

using ParkingApp.Domain.ValueObjects;
using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.Corporate.UnitTests;

public class WaitlistPromotionServiceTests
{
    private readonly Mock<ICorporateUnitOfWork> _corporate = new();
    private readonly Mock<IMarketplaceBookingService> _marketplaceBookings = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IWaitlistPromotionStore> _store = new();

    public WaitlistPromotionServiceTests()
    {
        _corporate.Setup(u => u.Companies).Returns(_companies.Object);
    }

    private WaitlistPromotionService CreateSut() => new(
        _corporate.Object,
        _store.Object,
        _marketplaceBookings.Object,
        _cache.Object,
        NullLogger<WaitlistPromotionService>.Instance);

    [Fact]
    public async Task PromoteAsync_WhenCompanyMissing_ReturnsFailure()
    {
        _companies.Setup(c => c.GetAggregateForWaitlistPromotionAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = CreateSut();
        var result = await sut.PromoteAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Company not found");
    }

    [Fact]
    public async Task PromoteAsync_WhenWaitlistEntryMissing_ReturnsFailure()
    {
        // Current service validates waitlist entry on the aggregate before admin-specific messages.
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);

        _companies.Setup(c => c.GetAggregateForWaitlistPromotionAsync(
                companyId, It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var sut = CreateSut();
        var result = await sut.PromoteAsync(companyId, Guid.NewGuid(), adminUserId: adminId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Waitlist entry not found");
    }

    [Fact]
    public async Task ProcessPendingAsync_ExpiresAndAttemptsCandidates()
    {
        var candidate = new WaitlistPromotionCandidate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            5,
            DateTime.UtcNow.AddMinutes(-10));

        _store.Setup(s => s.ExpireStalePendingAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _store.Setup(s => s.GetPromotionCandidatesAsync(It.IsAny<DateTime>(), 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WaitlistPromotionCandidate> { candidate });

        // Promote will fail early (company not found) G counts as skipped/attempted
        _companies.Setup(c => c.GetAggregateForWaitlistPromotionAsync(
                candidate.CompanyId, candidate.WaitlistEntryId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = CreateSut();
        var result = await sut.ProcessPendingAsync(batchSize: 10);

        result.Expired.Should().Be(2);
        result.Attempted.Should().Be(1);
        result.Promoted.Should().Be(0);
        result.Skipped.Should().Be(1);
    }

    [Fact]
    public async Task PromoteAsync_WhenMembershipInactive_ReturnsFailure()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var membership = company.Memberships.First();
        membership.Deactivate();
        var alloc = ParkingAllocation.Create(companyId, Guid.NewGuid(), Quota.Create(10, 5, 5), 100m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        alloc.Approve(adminId);
        company.Allocations.Add(alloc);
        var entry = CorporateWaitlistEntry.CreateEmployee(companyId, membership.Id, alloc.Id, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), VehicleType.Car, "MH01AB1234", 1);
        company.WaitlistEntries.Add(entry);

        _companies.Setup(c => c.GetAggregateForWaitlistPromotionAsync(
                companyId, entry.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var sut = CreateSut();
        var result = await sut.PromoteAsync(companyId, entry.Id, adminId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Employee membership is no longer active");
    }

    [Fact]
    public async Task PromoteAsync_WhenAllocationInactive_ReturnsFailure()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var membership = company.Memberships.First();
        var alloc = ParkingAllocation.Create(companyId, Guid.NewGuid(), Quota.Create(10, 5, 5), 100m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        // Allocation is Pending, not Approved/Active
        company.Allocations.Add(alloc);
        var entry = CorporateWaitlistEntry.CreateEmployee(companyId, membership.Id, alloc.Id, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), VehicleType.Car, "MH01AB1234", 1);
        company.WaitlistEntries.Add(entry);

        _companies.Setup(c => c.GetAggregateForWaitlistPromotionAsync(
                companyId, entry.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var sut = CreateSut();
        var result = await sut.PromoteAsync(companyId, entry.Id, adminId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Allocation no longer valid");
    }

    [Fact]
    public async Task PromoteAsync_EmployeePromotion_Success()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var membership = company.Memberships.First();
        var alloc = ParkingAllocation.Create(companyId, Guid.NewGuid(), Quota.Create(10, 2, 8), 100m, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddMonths(2));
        alloc.Approve(adminId);
        company.Allocations.Add(alloc);

        var nextMonday = DateTime.UtcNow.Date.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow.Date) nextMonday = nextMonday.AddDays(7);
        var start = nextMonday.AddHours(9);
        var end = nextMonday.AddHours(17);

        var entry = CorporateWaitlistEntry.CreateEmployee(companyId, membership.Id, alloc.Id, start, end, VehicleType.Car, "MH01AB1234", 1);
        company.WaitlistEntries.Add(entry);

        _companies.Setup(c => c.GetAggregateForWaitlistPromotionAsync(companyId, entry.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _cache.Setup(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _corporate.Setup(c => c.CorporateBookings.GetActiveSharedBookingsCountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _corporate.Setup(c => c.CorporateBookings.GetOccupiedSharedSlotNumbersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int>());
        _corporate.Setup(c => c.CorporateBookings.GetSharedSlotUsageCountsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _corporate.Setup(c => c.CorporateBookings.GetRecentBookingCreateCountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = CreateSut();
        var result = await sut.PromoteAsync(companyId, entry.Id, adminId);

        result.Success.Should().BeTrue(result.Message);
        result.Message.Should().Contain("promoted");
    }

    [Fact]
    public async Task PromoteAsync_VisitorPromotion_Success()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var membership = company.Memberships.First();
        var alloc = ParkingAllocation.Create(companyId, Guid.NewGuid(), Quota.Create(10, 2, 8), 100m, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddMonths(2));
        alloc.Approve(adminId);
        company.Allocations.Add(alloc);

        var nextMonday = DateTime.UtcNow.Date.AddDays(((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7);
        if (nextMonday <= DateTime.UtcNow.Date) nextMonday = nextMonday.AddDays(7);
        var start = nextMonday.AddHours(9);
        var end = nextMonday.AddHours(17);

        var entry = CorporateWaitlistEntry.CreateVisitor(companyId, membership.Id, alloc.Id, start, end, "Guest John", "MH01V1234", end.AddHours(1), 1, VehicleType.Car);
        company.WaitlistEntries.Add(entry);

        _companies.Setup(c => c.GetAggregateForWaitlistPromotionAsync(companyId, entry.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _cache.Setup(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _corporate.Setup(c => c.CorporateBookings.GetActiveSharedBookingsCountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _corporate.Setup(c => c.CorporateBookings.GetOccupiedSharedSlotNumbersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int>());
        _corporate.Setup(c => c.CorporateBookings.GetSharedSlotUsageCountsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());

        var sut = CreateSut();
        var result = await sut.PromoteAsync(companyId, entry.Id, adminUserId: null);

        result.Success.Should().BeTrue(result.Message);
        result.Message.Should().Contain("auto-promoted");
    }


    [Fact]
    public async Task PromoteAsync_WhenDomainExceptionOccurs_ReturnsFailureMessage()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var membership = company.Memberships.First();
        var alloc = ParkingAllocation.Create(companyId, Guid.NewGuid(), Quota.Create(10, 5, 5), 100m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMonths(1));
        alloc.Approve(adminId);
        company.Allocations.Add(alloc);
        var entry = CorporateWaitlistEntry.CreateEmployee(companyId, membership.Id, alloc.Id, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), VehicleType.Car, "MH01AB1234", 1);
        company.WaitlistEntries.Add(entry);

        _companies.Setup(c => c.GetAggregateForWaitlistPromotionAsync(companyId, entry.Id, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _cache.Setup(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _corporate.Setup(c => c.CorporateBookings.GetActiveSharedBookingsCountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated error"));

        var sut = CreateSut();
        var result = await sut.PromoteAsync(companyId, entry.Id, adminId);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Simulated error");
    }
}







