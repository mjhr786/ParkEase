using FluentAssertions;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Events;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class OverstayAutoCheckOutTests
{
    [Fact]
    public void ShouldAutoCheckOut_False_BeforeGracePlusDelay()
    {
        var end = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        // Grace 15 + auto 60 → cutoff 11:15
        var now = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);

        OverstayDetectionService.ShouldAutoCheckOut(end, now, graceMinutes: 15, autoCheckOutMinutes: 60)
            .Should().BeFalse();
    }

    [Fact]
    public void ShouldAutoCheckOut_True_AtOrAfterCutoff()
    {
        var end = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 1, 11, 15, 0, DateTimeKind.Utc);

        OverstayDetectionService.ShouldAutoCheckOut(end, now, graceMinutes: 15, autoCheckOutMinutes: 60)
            .Should().BeTrue();
    }

    [Fact]
    public void ShouldAutoCheckOut_ZeroAutoMinutes_TrueImmediatelyAfterGrace()
    {
        var end = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 1, 1, 10, 15, 0, DateTimeKind.Utc);

        OverstayDetectionService.ShouldAutoCheckOut(end, now, graceMinutes: 15, autoCheckOutMinutes: 0)
            .Should().BeTrue();
    }

    [Fact]
    public void CheckOut_FromInProgress_CompletesAndRaisesEvent()
    {
        var booking = new Booking
        {
            Status = BookingStatus.InProgress,
            EndDateTime = DateTime.UtcNow.AddHours(-2)
        };
        var at = DateTime.UtcNow;

        booking.CheckOut(at);

        booking.Status.Should().Be(BookingStatus.Completed);
        booking.CheckOutTime.Should().Be(at);
        booking.DomainEvents.Should().ContainSingle(e => e is BookingCheckedOutEvent);
    }

    [Fact]
    public void CanRequestExtension_True_WhenInProgressWithoutPendingExtension()
    {
        var booking = new Booking { Status = BookingStatus.InProgress };
        OverstayDetectionService.CanRequestExtension(booking).Should().BeTrue();
    }

    [Fact]
    public void CanRequestExtension_False_WhenPendingExtension()
    {
        var booking = new Booking
        {
            Status = BookingStatus.PendingExtension,
            PendingExtensionEndDateTime = DateTime.UtcNow.AddHours(1),
            PendingExtensionAmount = 50m
        };
        OverstayDetectionService.CanRequestExtension(booking).Should().BeFalse();
    }
}
