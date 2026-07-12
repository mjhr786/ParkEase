using FluentAssertions;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Services;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class BookingAvailabilityRulesTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = Now.AddHours(2);
    private static readonly DateTime End = Now.AddHours(4);

    private static ParkingSpace ActiveParking(int totalSpots = 5) =>
        new() { Id = Guid.NewGuid(), IsActive = true, TotalSpots = totalSpots };

    private static Booking ActiveBooking(
        DateTime start,
        DateTime end,
        string? vehicle = null,
        int? slot = null,
        BookingStatus status = BookingStatus.Confirmed,
        string? reference = "BK-TEST")
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            StartDateTime = start,
            EndDateTime = end,
            Status = status,
            VehicleNumber = vehicle,
            SlotNumber = slot,
            BookingReference = reference
        };
        return booking;
    }

    [Fact]
    public void ValidateTimeWindow_EndBeforeStart_Fails()
    {
        var result = BookingAvailabilityRules.ValidateTimeWindow(End, Start, Now);
        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Be("End date must be after start date");
    }

    [Fact]
    public void ValidateTimeWindow_StartInPast_Fails()
    {
        var result = BookingAvailabilityRules.ValidateTimeWindow(Now.AddHours(-1), Now.AddHours(1), Now);
        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Be("Start date must be in the future");
    }

    [Fact]
    public void ValidateTimeWindow_Valid_Ok()
    {
        BookingAvailabilityRules.ValidateTimeWindow(Start, End, Now).IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void ValidateSlotNumber_OutOfRange_Fails(int slot)
    {
        var result = BookingAvailabilityRules.ValidateSlotNumber(slot, 5);
        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Slot must be between 1 and 5");
    }

    [Fact]
    public void ValidateSlotNumber_Null_Ok()
    {
        BookingAvailabilityRules.ValidateSlotNumber(null, 5).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateVehicleOverlap_SameVehicleActive_Fails()
    {
        var existing = ActiveBooking(Start, End, "KA01AB1234", reference: "BK-OLD");
        var result = BookingAvailabilityRules.ValidateVehicleOverlap(
            "ka01ab1234", new[] { existing }, Start.AddMinutes(30), End.AddMinutes(30));

        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already booked");
        result.ErrorMessage.Should().Contain("BK-OLD");
    }

    [Fact]
    public void ValidateVehicleOverlap_CancelledBooking_Ok()
    {
        var existing = ActiveBooking(Start, End, "KA01AB1234", status: BookingStatus.Cancelled);
        var result = BookingAvailabilityRules.ValidateVehicleOverlap(
            "KA01AB1234", new[] { existing }, Start, End);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateCapacity_Full_Fails()
    {
        var result = BookingAvailabilityRules.ValidateCapacity(true, 3, 3);
        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Be("No spots available for the selected time");
    }

    [Fact]
    public void ValidateCapacity_NoOverlap_Ok()
    {
        BookingAvailabilityRules.ValidateCapacity(false, 10, 1).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateSlotConflict_SameSlotActive_Fails()
    {
        var existing = ActiveBooking(Start, End, slot: 2);
        var result = BookingAvailabilityRules.ValidateSlotConflict(2, new[] { existing });

        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Be("Slot 2 is already booked for the selected time");
    }

    [Fact]
    public void ValidateSlotConflict_ExcludeSelf_Ok()
    {
        var existing = ActiveBooking(Start, End, slot: 2);
        var result = BookingAvailabilityRules.ValidateSlotConflict(2, new[] { existing }, existing.Id);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateCreateFacts_InactiveParking_Fails()
    {
        var parking = new ParkingSpace { IsActive = false, TotalSpots = 5 };
        var result = BookingAvailabilityRules.ValidateCreateFacts(
            parking, Start, End, Now, null, null,
            Array.Empty<Booking>(), false, 0, Array.Empty<Booking>());

        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Be("Parking space is not available");
    }

    [Fact]
    public void ValidateCreateFacts_HappyPath_Ok()
    {
        var result = BookingAvailabilityRules.ValidateCreateFacts(
            ActiveParking(), Start, End, Now, 1, "KA01",
            Array.Empty<Booking>(), false, 0, Array.Empty<Booking>());

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ValidateRescheduleFacts_FullCapacity_UsesRescheduleMessage()
    {
        var result = BookingAvailabilityRules.ValidateRescheduleFacts(
            ActiveParking(1), Start, End, Now, hasSpaceOverlap: true, activeSpaceBookingCount: 1);

        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Be("No spots available for new dates");
    }

    [Fact]
    public void TimeRangesOverlap_TouchingEdges_DoNotOverlap()
    {
        // endA == startB is not overlap (half-open style used by handler: start < endB && end > startB)
        BookingAvailabilityRules.TimeRangesOverlap(Start, End, End, End.AddHours(1)).Should().BeFalse();
        BookingAvailabilityRules.TimeRangesOverlap(Start, End, Start.AddHours(1), End.AddHours(1)).Should().BeTrue();
    }
}
