using ParkingApp.BuildingBlocks.Domain;
using System;

namespace ParkingApp.Marketplace.Domain.Entities;

public class Favorite : BaseEntity
{
    public Guid UserId { get; set; }

    public Guid ParkingSpaceId { get; set; }
    public virtual ParkingSpace ParkingSpace { get; set; } = null!;
}
