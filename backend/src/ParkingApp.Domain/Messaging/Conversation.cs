using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Shared;
namespace ParkingApp.Domain.Messaging;

public class Conversation : BaseEntity
{
    public Guid ParkingSpaceId { get; set; }
    public Guid UserId { get; set; }       // Member who initiated the conversation
    public Guid VendorId { get; set; }      // Parking space owner
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; } // First 100 chars of latest message

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual User Vendor { get; set; } = null!;
    public virtual ParkingSpace ParkingSpace { get; set; } = null!;
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
