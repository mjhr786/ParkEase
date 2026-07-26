using FluentAssertions;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class SessionReminderTests
{
    [Fact]
    public void TryMarkSessionEndReminded_Once_ForInProgress()
    {
        var booking = new Booking { Status = BookingStatus.InProgress };
        var now = DateTime.UtcNow;

        booking.TryMarkSessionEndReminded(now).Should().BeTrue();
        booking.SessionEndRemindedAt.Should().Be(now);
        booking.TryMarkSessionEndReminded(now.AddMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void TryMarkSessionEndReminded_AllowsConfirmed()
    {
        var booking = new Booking { Status = BookingStatus.Confirmed };
        booking.TryMarkSessionEndReminded(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void TryMarkSessionEndReminded_RejectsCompleted()
    {
        var booking = new Booking { Status = BookingStatus.Completed };
        booking.TryMarkSessionEndReminded(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void ConfirmExtension_ClearsSessionEndRemindedAt()
    {
        var booking = new Booking
        {
            Status = BookingStatus.InProgress,
            EndDateTime = DateTime.UtcNow.AddMinutes(30),
            SessionEndRemindedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var newEnd = booking.EndDateTime.AddHours(1);
        booking.RequestExtension(newEnd, 0m);
        booking.ConfirmExtension();

        booking.SessionEndRemindedAt.Should().BeNull();
        booking.EndDateTime.Should().Be(newEnd);
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

        SessionReminderService.CanRequestExtension(booking).Should().BeFalse();
    }

    [Fact]
    public void CanRequestExtension_True_WhenInProgressWithoutPending()
    {
        var booking = new Booking { Status = BookingStatus.InProgress };
        SessionReminderService.CanRequestExtension(booking).Should().BeTrue();
    }
}
