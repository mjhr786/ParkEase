using FluentAssertions;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Domain;

public class BayGuidanceAndValetTests
{
    [Fact]
    public void AssignBayGuidance_SetsLabels_AndSlotBayLabel()
    {
        var booking = new Booking { Status = BookingStatus.Confirmed };
        booking.AssignBayGuidance("P2", "Blue", null, slotNumber: 14);

        booking.FacilityLevel.Should().Be("P2");
        booking.FacilityZone.Should().Be("Blue");
        booking.SlotNumber.Should().Be(14);
        booking.BayLabel.Should().Be("B-14");
    }

    [Fact]
    public void TryAutoAssignBayFromFacility_OnlyWhenEnabledAndEmpty()
    {
        var booking = new Booking { Status = BookingStatus.Confirmed };
        booking.TryAutoAssignBayFromFacility(true, "P1", "Green", 3).Should().BeTrue();
        booking.BayLabel.Should().Be("B-3");
        booking.FacilityLevel.Should().Be("P1");

        booking.TryAutoAssignBayFromFacility(true, "P9", "Red", 9).Should().BeFalse();
        booking.FacilityLevel.Should().Be("P1");
    }

    [Fact]
    public void RequestValet_Lifecycle_AndIdempotency()
    {
        var booking = new Booking { Status = BookingStatus.InProgress };
        var now = DateTime.UtcNow;

        booking.RequestValet(now, 10, "Curbside");
        booking.ValetStatus.Should().Be(ValetStatus.Requested);
        booking.ValetTargetReadyAt.Should().Be(now.AddMinutes(10));
        booking.ValetNotes.Should().Be("Curbside");

        var act = () => booking.RequestValet(now.AddMinutes(1), 10);
        act.Should().Throw<BusinessRuleException>();

        booking.AcknowledgeValet();
        booking.ValetStatus.Should().Be(ValetStatus.InProgress);

        booking.MarkValetReady(now.AddMinutes(8));
        booking.ValetStatus.Should().Be(ValetStatus.Ready);
        booking.ValetReadyAt.Should().Be(now.AddMinutes(8));

        booking.CompleteValet();
        booking.ValetStatus.Should().Be(ValetStatus.Completed);
    }

    [Fact]
    public void CancelValet_FromRequested()
    {
        var booking = new Booking { Status = BookingStatus.Confirmed };
        booking.RequestValet(DateTime.UtcNow, 10);
        booking.CancelValet();
        booking.ValetStatus.Should().Be(ValetStatus.Cancelled);

        // Can request again after cancel
        booking.RequestValet(DateTime.UtcNow, 5);
        booking.ValetStatus.Should().Be(ValetStatus.Requested);
    }

    [Fact]
    public void ParkingSpace_SetBayAndValet_SyncsAmenities()
    {
        var space = ParkingSpace.CreateForVendor(
            Guid.NewGuid(),
            "Garage",
            "Multi-level",
            "1 Main",
            "City",
            "ST",
            "IN",
            "100001",
            12.9,
            77.6,
            ParkingType.Covered,
            50,
            50,
            400,
            2000,
            6000);

        space.SetBayAndValet(true, true, "P2", "Blue", "Enter ramp B");
        space.IsBayGuidanceEnabled.Should().BeTrue();
        space.IsValetEnabled.Should().BeTrue();
        space.DefaultFacilityLevel.Should().Be("P2");
        space.IndoorGuidanceNotes.Should().Be("Enter ramp B");
        space.Amenities.Should().Contain("Bay Guidance");
        space.Amenities.Should().Contain("Valet");

        space.SetBayAndValet(false, false, null, null, null);
        space.IsBayGuidanceEnabled.Should().BeFalse();
        space.Amenities?.Contains("Valet").Should().BeFalse();
    }
}
