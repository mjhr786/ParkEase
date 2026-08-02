namespace ParkingApp.BuildingBlocks.Enums;

/// <summary>
/// Coarse parking bay category used by corporate capacity pools.
/// Maps from <see cref="VehicleType"/> via <see cref="VehicleClassMapper"/>.
/// </summary>
public enum VehicleClass
{
    TwoWheeler = 1,
    FourWheeler = 2
}
