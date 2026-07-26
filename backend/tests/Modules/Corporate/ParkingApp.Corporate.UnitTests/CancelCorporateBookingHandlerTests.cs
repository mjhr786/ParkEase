using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Bookings;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Corporate.UnitTests;

public class CancelCorporateBookingHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _corporate = new();
    private readonly Mock<IMarketplaceBookingService> _marketplaceBookings = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<ICompanyQuotaCache> _quotaCache = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICorporateBookingRepository> _corporateBookings = new();

    public CancelCorporateBookingHandlerTests()
    {
        _corporate.Setup(c => c.Companies).Returns(_companies.Object);
        _corporate.Setup(c => c.CorporateBookings).Returns(_corporateBookings.Object);
        _corporate.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _cache.Setup(c => c.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _quotaCache.Setup(q => q.InvalidateCompanyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    private CancelCorporateBookingHandler CreateHandler() =>
        new(_corporate.Object, _marketplaceBookings.Object, _cache.Object, _quotaCache.Object);

    [Fact]
    public async Task Handle_WhenNotMember_Denies()
    {
        _companies.Setup(c => c.GetMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCompanyMembership?)null);

        var result = await CreateHandler().HandleAsync(new CancelCorporateBookingCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "reason"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Access denied");
    }

    [Fact]
    public async Task Handle_WhenEmployeeCancelsOtherMembershipBooking_Denies()
    {
        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var employeeMembership = company.AddMember(adminId, employeeId, CompanyRole.Employee);
        var otherMembership = company.AddMember(adminId, Guid.NewGuid(), CompanyRole.Employee);

        _companies.Setup(c => c.GetMembershipAsync(companyId, employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeMembership);

        var corpBooking = CorporateBooking.CreateEmployeeBooking(
            companyId, otherMembership.Id, Guid.NewGuid(), Guid.NewGuid(), CorporateSlotType.Shared);
        _corporateBookings.Setup(r => r.GetByCompanyAndBookingIdAsync(companyId, corpBooking.BookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(corpBooking);

        var result = await CreateHandler().HandleAsync(new CancelCorporateBookingCommand(
            companyId, employeeId, corpBooking.BookingId, "nope"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("only cancel your own");
        _marketplaceBookings.Verify(
            m => m.CancelAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAdmin_CancelsConfirmedBooking()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var adminMembership = company.Memberships.First(m => m.UserId == adminId);
        var employeeMembership = company.AddMember(adminId, employeeId, CompanyRole.Employee);

        _companies.Setup(c => c.GetMembershipAsync(companyId, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminMembership);

        var bookingId = Guid.NewGuid();
        var parkingSpaceId = Guid.NewGuid();
        var snapshot = new BookingSnapshot(
            bookingId,
            employeeId,
            parkingSpaceId,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            nameof(BookingStatus.Cancelled),
            "CORP-1",
            SlotNumber: 2,
            TotalAmount: 0m,
            VehicleNumber: "KA01AB1234");

        var corpBooking = CorporateBooking.CreateEmployeeBooking(
            companyId, employeeMembership.Id, Guid.NewGuid(), bookingId, CorporateSlotType.Shared);
        _corporateBookings.Setup(r => r.GetByCompanyAndBookingIdAsync(companyId, bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(corpBooking);

        _marketplaceBookings.Setup(m => m.CancelAsync(bookingId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketplaceBookingCancelResult(true, "Booking cancelled.", snapshot));

        var result = await CreateHandler().HandleAsync(new CancelCorporateBookingCommand(
            companyId, adminId, bookingId, "Admin cancel"));

        result.Success.Should().BeTrue();
        result.Data!.BookingId.Should().Be(bookingId);
        _corporate.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _marketplaceBookings.Verify(m => m.CancelAsync(bookingId, "Admin cancel", It.IsAny<CancellationToken>()), Times.Once);
        _quotaCache.Verify(q => q.InvalidateCompanyAsync(companyId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
