using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Bookings;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Marketplace;
using Xunit;

namespace ParkingApp.UnitTests.Corporate;

public class CancelCorporateBookingHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _corporate = new();
    private readonly Mock<IMarketplaceUnitOfWork> _marketplace = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICorporateBookingRepository> _corporateBookings = new();
    private readonly Mock<IBookingRepository> _bookings = new();

    public CancelCorporateBookingHandlerTests()
    {
        _corporate.Setup(c => c.Companies).Returns(_companies.Object);
        _corporate.Setup(c => c.CorporateBookings).Returns(_corporateBookings.Object);
        _marketplace.Setup(m => m.Bookings).Returns(_bookings.Object);
    }

    [Fact]
    public async Task Handle_WhenNotMember_Denies()
    {
        _companies.Setup(c => c.GetMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCompanyMembership?)null);

        var handler = new CancelCorporateBookingHandler(_corporate.Object, _marketplace.Object);
        var result = await handler.HandleAsync(new CancelCorporateBookingCommand(
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

        var handler = new CancelCorporateBookingHandler(_corporate.Object, _marketplace.Object);
        var result = await handler.HandleAsync(new CancelCorporateBookingCommand(
            companyId, employeeId, corpBooking.BookingId, "nope"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("only cancel your own");
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
        var booking = Booking.CreateCorporateEmployee(
            employeeId,
            parkingSpaceId,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            VehicleType.Car,
            0m,
            "KA01AB1234");
        // Force known id for matching
        typeof(Booking).GetProperty("Id")!.SetValue(booking, bookingId);

        var corpBooking = CorporateBooking.CreateEmployeeBooking(
            companyId, employeeMembership.Id, Guid.NewGuid(), bookingId, CorporateSlotType.Shared);
        _corporateBookings.Setup(r => r.GetByCompanyAndBookingIdAsync(companyId, bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(corpBooking);
        _bookings.Setup(b => b.GetByIdWithDetailsAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var handler = new CancelCorporateBookingHandler(_corporate.Object, _marketplace.Object);
        var result = await handler.HandleAsync(new CancelCorporateBookingCommand(
            companyId, adminId, bookingId, "Admin cancel"));

        result.Success.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        _corporate.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
