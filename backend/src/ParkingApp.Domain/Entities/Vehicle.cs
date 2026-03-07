using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Entities;

public class Vehicle : BaseEntity
{
    public Guid UserId { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public VehicleType Type { get; set; }
    public bool IsDefault { get; set; } = false;

    // Navigation property
    public virtual User User { get; set; } = null!;
}
