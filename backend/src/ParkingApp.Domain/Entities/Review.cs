namespace ParkingApp.Domain.Entities;

public class Review : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ParkingSpaceId { get; set; }
    public Guid? BookingId { get; set; }
    
    public int Rating { get; set; } // 1-5
    public string? Title { get; set; }
    public string? Comment { get; set; }
    
    // Helpful votes
    public int HelpfulCount { get; set; } = 0;
    
    // Moderation
    public bool IsApproved { get; set; } = true;
    public bool IsReported { get; set; } = false;
    
    // Owner response
    public string? OwnerResponse { get; set; }
    public DateTime? OwnerResponseAt { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual ParkingSpace ParkingSpace { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
}
