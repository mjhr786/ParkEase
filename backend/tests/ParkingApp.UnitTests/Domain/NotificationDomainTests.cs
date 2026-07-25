using FluentAssertions;
using ParkingApp.Messaging.Contracts.Enums;
using ParkingApp.Messaging.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class NotificationDomainTests
{
    [Fact]
    public void MarkAsRead_IsIdCentric_AndIdempotent()
    {
        var notification = new Notification
        {
            UserId = Guid.NewGuid(),
            Type = NotificationType.SystemAlert,
            Title = "Hello",
            Message = "World"
        };

        notification.MarkAsRead();
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();

        var firstReadAt = notification.ReadAt;
        notification.MarkAsRead(DateTime.UtcNow.AddHours(1));
        notification.ReadAt.Should().Be(firstReadAt);
    }
}





