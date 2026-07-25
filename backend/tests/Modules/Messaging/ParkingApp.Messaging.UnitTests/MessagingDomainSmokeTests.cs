using FluentAssertions;
using ParkingApp.Messaging.Domain.Entities;

namespace ParkingApp.Messaging.UnitTests;

public class MessagingDomainSmokeTests
{
    [Fact]
    public void Conversation_CanBeConstructed_WithParticipantIds()
    {
        var parkingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vendorId = Guid.NewGuid();

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            ParkingSpaceId = parkingId,
            UserId = userId,
            VendorId = vendorId
        };

        conversation.ParkingSpaceId.Should().Be(parkingId);
        conversation.UserId.Should().Be(userId);
        conversation.VendorId.Should().Be(vendorId);
    }
}
