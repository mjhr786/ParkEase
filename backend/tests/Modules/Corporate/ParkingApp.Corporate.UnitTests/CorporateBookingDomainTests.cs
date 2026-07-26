using FluentAssertions;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Corporate.UnitTests;

public class CorporateBookingDomainTests
{
    [Fact]
    public void CreateEmployeeBooking_SetsCorporateContext()
    {
        var companyId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var booking = CorporateBooking.CreateEmployeeBooking(
            companyId, membershipId, allocationId, bookingId, CorporateSlotType.Fixed);

        booking.CompanyId.Should().Be(companyId);
        booking.MembershipId.Should().Be(membershipId);
        booking.AllocationId.Should().Be(allocationId);
        booking.BookingId.Should().Be(bookingId);
        booking.SlotType.Should().Be(CorporateSlotType.Fixed);
        booking.IsVisitorBooking.Should().BeFalse();
        booking.AccessPolicy.Should().BeNull();
    }

    [Fact]
    public void CreateVisitorBooking_NormalizesPlateAndRequiresPolicy()
    {
        var policy = AccessPolicy.Create(
            "ka01xx9999",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(4));

        var booking = CorporateBooking.CreateVisitorBooking(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  Guest User ",
            " ka01xx9999 ",
            policy);

        booking.IsVisitorBooking.Should().BeTrue();
        booking.VisitorName.Should().Be("Guest User");
        booking.VisitorLicensePlate.Should().Be("KA01XX9999");
        booking.SlotType.Should().Be(CorporateSlotType.Shared);
        booking.AccessPolicy.Should().NotBeNull();
    }

    [Fact]
    public void CreateVisitorBooking_RejectsEmptyName()
    {
        var policy = AccessPolicy.Create("KA01AB1234", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        var act = () => CorporateBooking.CreateVisitorBooking(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "  ", "KA01AB1234", policy);

        act.Should().Throw<ArgumentException>().WithMessage("*Visitor name*");
    }

    [Fact]
    public void CreateEmployeeBooking_RejectsEmptyIds()
    {
        var act = () => CorporateBooking.CreateEmployeeBooking(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CorporateSlotType.Shared);

        act.Should().Throw<ArgumentException>();
    }
}
