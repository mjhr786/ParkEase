using ParkingApp.Application.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Corporate.Application.Services;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Corporate.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Corporate;

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

        // Promote will fail early (company not found) G�� counts as skipped/attempted
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
}






