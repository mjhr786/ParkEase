using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Entities;

public class ParkingSpace : BaseEntity
{
    public Guid OwnerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Location
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    // Parking details
    public ParkingType ParkingType { get; set; }
    public int TotalSpots { get; set; } = 1;
    public int AvailableSpots { get; set; } = 1;
    
    // Pricing
    public decimal HourlyRate { get; set; }
    public decimal DailyRate { get; set; }
    public decimal WeeklyRate { get; set; }
    public decimal MonthlyRate { get; set; }
    
    // Availability
    public TimeSpan OpenTime { get; set; } = new TimeSpan(0, 0, 0);
    public TimeSpan CloseTime { get; set; } = new TimeSpan(23, 59, 59);
    public bool Is24Hours { get; set; } = true;
    public string? AvailableDays { get; set; } = "1,2,3,4,5,6,7"; // 1=Monday, 7=Sunday
    
    // Features/Amenities (stored as comma-separated)
    public string? Amenities { get; set; } // CCTV,Security,EV_Charging,Covered,24x7
    public string? AllowedVehicleTypes { get; set; } // Car,Motorcycle,SUV
    
    // Media
    public string? ImageUrls { get; set; } // Comma-separated URLs
    
    // Status
    public bool IsActive { get; set; } = true;
    public bool IsVerified { get; set; } = false;
    
    // Rating
    public double AverageRating { get; set; } = 0;
    public int TotalReviews { get; set; } = 0;
    
    // Instructions
    public string? SpecialInstructions { get; set; }
    
    // Navigation properties
    public virtual User Owner { get; set; } = null!;
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    public virtual ICollection<ParkingAvailability> Availabilities { get; set; } = new List<ParkingAvailability>();
}

public class ParkingAvailability : BaseEntity
{
    public Guid ParkingSpaceId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int AvailableSpots { get; set; }
    
    // Navigation
    public virtual ParkingSpace ParkingSpace { get; set; } = null!;
}
