namespace ParkingApp.BuildingBlocks.Enums;

/// <summary>
/// Maps fine-grained vehicle types to corporate 2W / 4W capacity pools.
/// </summary>
public static class VehicleClassMapper
{
    /// <summary>
    /// Motorcycle → TwoWheeler; all other current types → FourWheeler.
    /// Electric scooters should be registered as <see cref="VehicleType.Motorcycle"/>.
    /// </summary>
    public static VehicleClass ToVehicleClass(VehicleType type) =>
        type == VehicleType.Motorcycle
            ? VehicleClass.TwoWheeler
            : VehicleClass.FourWheeler;

    /// <summary>
    /// SQL / filter: marketplace <c>Bookings.VehicleType</c> for TwoWheeler is Motorcycle (1).
    /// </summary>
    public const int TwoWheelerVehicleTypeValue = (int)VehicleType.Motorcycle;

    /// <summary>
    /// Mirrors corporate occupancy SQL: TwoWheeler counts only Motorcycle;
    /// FourWheeler counts null VehicleType (legacy) and all non-Motorcycle types.
    /// Keep in sync with <c>CompanyRepository.GetReservationPreCheckAsync</c> class filters.
    /// </summary>
    public static bool BookingMatchesClass(int? vehicleTypeValue, VehicleClass vehicleClass) =>
        vehicleClass == VehicleClass.TwoWheeler
            ? vehicleTypeValue == TwoWheelerVehicleTypeValue
            : vehicleTypeValue is null || vehicleTypeValue != TwoWheelerVehicleTypeValue;

    /// <inheritdoc cref="BookingMatchesClass(int?, VehicleClass)"/>
    public static bool BookingMatchesClass(VehicleType? vehicleType, VehicleClass vehicleClass) =>
        BookingMatchesClass(vehicleType.HasValue ? (int)vehicleType.Value : null, vehicleClass);
}
