using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Corporate;

public class WaitlistPromotionServiceTests
{
    private readonly Mock<ICorporateUnitOfWork> _corporate = new();
    private readonly Mock<IMarketplaceUnitOfWork> _marketplace = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<ICompanyQuotaCache> _quota = new();
    private readonly Mock<IWaitlistPromotionStore> _store = new();

    public WaitlistPromotionServiceTests()
    {
        _corporate.Setup(u => u.Companies).Returns(_companies.Object);
    }

    private WaitlistPromotionService CreateSut() => new(
        _corporate.Object,
        _marketplace.Object,
        _cache.Object,
        _quota.Object,
        _store.Object,
        NullLogger<WaitlistPromotionService>.Instance);

    [Fact]
    public async Task PromoteAsync_WhenCompanyMissing_ReturnsFailure()
    {
        _companies.Setup(c => c.GetFullAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = CreateSut();
        var result = await sut.PromoteAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Company not found");
    }

    [Fact]
    public async Task PromoteAsync_WhenAdminRequiredAndUserNotAdmin_ReturnsFailure()
    {
        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        // employeeId is not an admin of this company
        company.AddMember(adminId, employeeId, CompanyRole.Employee);

        _companies.Setup(c => c.GetFullAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var sut = CreateSut();
        var result = await sut.PromoteAsync(companyId, Guid.NewGuid(), adminUserId: employeeId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Only company admins");
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

        // Promote will fail early (company not found) — counts as skipped/attempted
        _companies.Setup(c => c.GetFullAsync(candidate.CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = CreateSut();
        var result = await sut.ProcessPendingAsync(batchSize: 10);

        result.Expired.Should().Be(2);
        result.Attempted.Should().Be(1);
        result.Promoted.Should().Be(0);
        result.Skipped.Should().Be(1);
    }
}
