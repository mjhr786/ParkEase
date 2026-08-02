using FluentAssertions;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Domain.ValueObjects;
using Xunit;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>Application helper: ResolveClassQuotas legacy vs nested dual pools (M3/M14).</summary>
public class CorporateCommandHelpersTests
{
    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void ResolveClassQuotas_NestedBothPools_UsesNested()
    {
        var (two, four) = CorporateCommandHelpers.ResolveClassQuotas(
            new SlotPoolDto(10, 2, 8),
            new SlotPoolDto(20, 5, 15),
            legacyTotalSlots: 99,
            legacyFixedSlots: 9,
            legacySharedSlots: 90);

        two.TotalSlots.Should().Be(10);
        two.FixedSlots.Should().Be(2);
        two.SharedSlots.Should().Be(8);
        four.TotalSlots.Should().Be(20);
        four.FixedSlots.Should().Be(5);
        four.SharedSlots.Should().Be(15);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void ResolveClassQuotas_OnlyFourWheelerNested_TwoWheelerNone()
    {
        var (two, four) = CorporateCommandHelpers.ResolveClassQuotas(
            twoWheeler: null,
            fourWheeler: new SlotPoolDto(5, 1, 4),
            legacyTotalSlots: 0,
            legacyFixedSlots: 0,
            legacySharedSlots: 0);

        two.IsEmpty.Should().BeTrue();
        four.TotalSlots.Should().Be(5);
        four.FixedSlots.Should().Be(1);
        four.SharedSlots.Should().Be(4);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void ResolveClassQuotas_LegacyOnly_MapsToFourWheeler()
    {
        var (two, four) = CorporateCommandHelpers.ResolveClassQuotas(
            twoWheeler: null,
            fourWheeler: null,
            legacyTotalSlots: 5,
            legacyFixedSlots: 1,
            legacySharedSlots: 4);

        two.IsEmpty.Should().BeTrue();
        two.Should().Be(Quota.None);
        four.TotalSlots.Should().Be(5);
        four.FixedSlots.Should().Be(1);
        four.SharedSlots.Should().Be(4);
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void ResolveClassQuotas_NeitherNestedNorLegacy_Throws()
    {
        var act = () => CorporateCommandHelpers.ResolveClassQuotas(
            null, null, legacyTotalSlots: 0, legacyFixedSlots: 0, legacySharedSlots: 0);

        act.Should().Throw<ArgumentException>().WithMessage("*TwoWheeler/FourWheeler*");
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void ResolveClassQuotas_NestedZeros_IgnoresLegacy()
    {
        // Nested present (even if empty) wins over legacy — domain will reject both empty later
        var (two, four) = CorporateCommandHelpers.ResolveClassQuotas(
            new SlotPoolDto(0, 0, 0),
            new SlotPoolDto(0, 0, 0),
            legacyTotalSlots: 10,
            legacyFixedSlots: 0,
            legacySharedSlots: 10);

        two.IsEmpty.Should().BeTrue();
        four.IsEmpty.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void BuildLockKey_IsDeterministic()
    {
        var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var allocationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var start = new DateTime(2026, 7, 22, 10, 30, 0, DateTimeKind.Utc);

        var key = CorporateCommandHelpers.BuildLockKey(companyId, allocationId, start);
        key.Should().Be($"lock:corp-booking:{companyId}:{allocationId}:2026072210");
    }
}
