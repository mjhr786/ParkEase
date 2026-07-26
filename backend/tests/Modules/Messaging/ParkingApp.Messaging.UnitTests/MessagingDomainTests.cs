using FluentAssertions;
using ParkingApp.Messaging.Contracts.Enums;
using ParkingApp.Messaging.Domain.Entities;

namespace ParkingApp.Messaging.UnitTests;

public class MessagingDomainTests
{
    [Fact]
    public void Conversation_TracksLastMessagePreview()
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            ParkingSpaceId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            VendorId = Guid.NewGuid()
        };

        var content = new string('x', 150);
        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.LastMessagePreview = content.Length > 100 ? content[..100] : content;

        conversation.LastMessagePreview.Should().HaveLength(100);
        conversation.LastMessageAt.Should().NotBeNull();
    }

    [Fact]
    public void ChatMessage_Defaults_IsUnread()
    {
        var message = new ChatMessage
        {
            ConversationId = Guid.NewGuid(),
            SenderId = Guid.NewGuid(),
            Content = "hello"
        };

        message.IsRead.Should().BeFalse();
        message.ReadAt.Should().BeNull();
        message.Content.Should().Be("hello");
    }

    [Fact]
    public void Notification_MarkAsRead_IsIdempotent()
    {
        var notification = new Notification
        {
            UserId = Guid.NewGuid(),
            Type = NotificationType.BookingConfirmed,
            Title = "Booked",
            Message = "Your booking is confirmed"
        };

        notification.IsRead.Should().BeFalse();
        notification.MarkAsRead(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().Be(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));

        var firstReadAt = notification.ReadAt;
        notification.MarkAsRead(DateTime.UtcNow.AddDays(1));
        notification.ReadAt.Should().Be(firstReadAt);
    }

    [Fact]
    public void Notification_DefaultPriority_IsNormal()
    {
        var notification = new Notification
        {
            UserId = Guid.NewGuid(),
            Type = NotificationType.SystemAlert,
            Title = "t",
            Message = "m"
        };

        notification.Priority.Should().Be(NotificationPriority.Normal);
    }
}
