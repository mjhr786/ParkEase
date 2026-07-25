using ParkingApp.BuildingBlocks.Domain;

namespace ParkingApp.Messaging.Domain.Entities;

/// <summary>
/// Chat message. Sender is referenced by <see cref="SenderId"/> only (no Identity navigation).
/// </summary>
public class ChatMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;
}
