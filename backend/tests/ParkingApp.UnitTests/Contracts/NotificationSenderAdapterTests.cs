using ParkingApp.Notifications.Contracts;
using FluentAssertions;
using Moq;
using ParkingApp.Application.Contracts.Notifications;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Infrastructure.ModuleAdapters;
using Xunit;

namespace ParkingApp.UnitTests.Contracts;

public class NotificationSenderAdapterTests
{
    [Fact]
    public async Task NotificationSender_Maps_Request_To_Coordinator()
    {
        var coordinator = new Mock<INotificationCoordinator>();
        NotificationRequest? captured = null;
        var userId = Guid.NewGuid();

        coordinator
            .Setup(c => c.SendAsync(userId, It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, NotificationRequest, CancellationToken>((_, req, _) => captured = req)
            .Returns(Task.CompletedTask);

        INotificationSender sender = new NotificationSender(coordinator.Object);
        await sender.SendAsync(userId, new NotificationSendRequest(
            "booking.cancelled",
            "Booking Cancelled",
            "REF-1 cancelled",
            Channels: new[] { "InApp", "Push" },
            Data: new Dictionary<string, string> { ["BookingId"] = "1" },
            Priority: "High"));

        captured.Should().NotBeNull();
        captured!.Type.Should().Be("booking.cancelled");
        captured.Title.Should().Be("Booking Cancelled");
        captured.Priority.Should().Be(NotificationPriority.High);
        captured.Channels.Should().HaveFlag(NotificationChannels.InApp);
        captured.Channels.Should().HaveFlag(NotificationChannels.Push);
        captured.Data.Should().ContainKey("BookingId");
    }
}






