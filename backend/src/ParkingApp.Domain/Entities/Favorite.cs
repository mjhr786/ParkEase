using System;

namespace ParkingApp.Domain.Entities;

public class Favorite : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public Guid ParkingSpaceId { get; set; }
    public virtual ParkingSpace ParkingSpace { get; set; } = null!;
}
