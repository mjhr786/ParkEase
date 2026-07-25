using ParkingApp.BuildingBlocks.Domain;

namespace ParkingApp.Messaging.Domain.Entities;

/// <summary>
/// Chat conversation. Cross-module parties referenced by ID only (UserId, VendorId, ParkingSpaceId).
/// </summary>
public class Conversation : BaseEntity
{
    public Guid ParkingSpaceId { get; set; }
    public Guid UserId { get; set; }
    public Guid VendorId { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }

    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
