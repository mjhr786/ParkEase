using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Enums;

namespace ParkingApp.Identity.Domain.Entities;

public class Vehicle : BaseEntity
{
    public Guid UserId { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public VehicleType Type { get; set; }
    public bool IsDefault { get; set; } = false;

    public virtual User User { get; set; } = null!;
}
