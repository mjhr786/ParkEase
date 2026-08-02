using FluentAssertions;
using ParkingApp.BuildingBlocks.Enums;
using Xunit;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>
/// L3 pragmatic proof for occupancy class filter (M13).
/// Mirrors <c>CompanyRepository.GetReservationPreCheckAsync</c> SQL predicates via
/// <see cref="VehicleClassMapper.BookingMatchesClass"/>.
/// </summary>
public class VehicleClassOccupancyFilterTests
{
    private static int CountMatching(
        IEnumerable<int?> vehicleTypes,
        VehicleClass vehicleClass) =>
        vehicleTypes.Count(vt => VehicleClassMapper.BookingMatchesClass(vt, vehicleClass));

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void MixedWindow_MotorcycleAndCar_CountPerClass()
    {
        // Seed: 1 Motorcycle + 1 Car shared in same window
        var types = new int?[]
        {
            (int)VehicleType.Motorcycle,
            (int)VehicleType.Car
        };

        CountMatching(types, VehicleClass.TwoWheeler).Should().Be(1);
        CountMatching(types, VehicleClass.FourWheeler).Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void NullVehicleType_CountsAsFourWheeler_NotTwoWheeler()
    {
        var types = new int?[] { null, (int)VehicleType.Motorcycle };

        CountMatching(types, VehicleClass.FourWheeler).Should().Be(1);
        CountMatching(types, VehicleClass.TwoWheeler).Should().Be(1);
        VehicleClassMapper.BookingMatchesClass((int?)null, VehicleClass.FourWheeler).Should().BeTrue();
        VehicleClassMapper.BookingMatchesClass((int?)null, VehicleClass.TwoWheeler).Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void AllNonMotorcycleTypes_AreFourWheeler()
    {
        foreach (var type in new[]
                 {
                     VehicleType.Car, VehicleType.SUV, VehicleType.Truck,
                     VehicleType.Van, VehicleType.Electric
                 })
        {
            VehicleClassMapper.BookingMatchesClass(type, VehicleClass.FourWheeler).Should().BeTrue();
            VehicleClassMapper.BookingMatchesClass(type, VehicleClass.TwoWheeler).Should().BeFalse();
        }
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void OccupancyFilter_DoesNotCrossConsume()
    {
        // Three bikes and zero cars → 4W availability unaffected by bike count
        var types = new int?[]
        {
            (int)VehicleType.Motorcycle,
            (int)VehicleType.Motorcycle,
            (int)VehicleType.Motorcycle
        };

        CountMatching(types, VehicleClass.TwoWheeler).Should().Be(3);
        CountMatching(types, VehicleClass.FourWheeler).Should().Be(0);
    }
}
