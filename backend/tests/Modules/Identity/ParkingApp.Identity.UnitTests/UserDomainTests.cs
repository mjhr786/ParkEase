using FluentAssertions;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Enums;

namespace ParkingApp.Identity.UnitTests;

public class UserDomainTests
{
    [Fact]
    public void Register_InvalidEmail_Throws()
    {
        var act = () => User.Register("not-an-email", "hash", "Ada", "Lovelace", "555");
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Register_MissingPasswordHash_Throws()
    {
        var act = () => User.Register("ada@example.com", " ", "Ada", "Lovelace", "555");
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void UpdateProfile_AndFullName()
    {
        var user = User.Register("ada@example.com", "hash", "Ada", "Lovelace", "555");
        user.UpdateProfile("Augusta", null, "999");
        user.FirstName.Should().Be("Augusta");
        user.LastName.Should().Be("Lovelace");
        user.PhoneNumber.Should().Be("999");
        user.FullName.Should().Be("Augusta Lovelace");
    }

    [Fact]
    public void RecordLogin_SetsTokenAndLastLogin()
    {
        var user = User.Register("ada@example.com", "hash", "Ada", "Lovelace", "555");
        var expiry = DateTime.UtcNow.AddDays(7);
        user.RecordLogin("refresh-1", expiry);

        user.RefreshToken.Should().Be("refresh-1");
        user.RefreshTokenExpiryTime.Should().Be(expiry);
        user.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordLogin_WhenDeactivated_Throws()
    {
        var user = User.Register("ada@example.com", "hash", "Ada", "Lovelace", "555");
        user.Deactivate();
        var act = () => user.RecordLogin("t", DateTime.UtcNow.AddDays(1));
        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void ChangePassword_RevokesRefreshToken()
    {
        var user = User.Register("ada@example.com", "hash", "Ada", "Lovelace", "555");
        user.RecordLogin("old", DateTime.UtcNow.AddDays(1));
        user.ChangePassword("new-hash");
        user.PasswordHash.Should().Be("new-hash");
        user.RefreshToken.Should().BeNull();
    }

    [Fact]
    public void Deactivate_RevokesToken_Activate_RestoresAccessFlag()
    {
        var user = User.Register("ada@example.com", "hash", "Ada", "Lovelace", "555");
        user.RecordLogin("t", DateTime.UtcNow.AddDays(1));
        user.Deactivate();
        user.IsActive.Should().BeFalse();
        user.RefreshToken.Should().BeNull();
        user.Activate();
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void MarkEmailAndPhoneVerified()
    {
        var user = User.Register("ada@example.com", "hash", "Ada", "Lovelace", "555");
        user.MarkEmailVerified();
        user.MarkPhoneVerified();
        user.IsEmailVerified.Should().BeTrue();
        user.IsPhoneVerified.Should().BeTrue();
    }

    [Fact]
    public void Vehicle_And_DeviceToken_CanAttachToUser()
    {
        var user = User.Register("ada@example.com", "hash", "Ada", "Lovelace", "555");
        var vehicle = new Vehicle
        {
            UserId = user.Id,
            LicensePlate = "KA01AB1",
            Make = "Tata",
            Model = "Nexon",
            Color = "Blue",
            Type = BuildingBlocks.Enums.VehicleType.Car,
            IsDefault = true
        };
        var device = new DeviceToken
        {
            UserId = user.Id,
            DeviceId = "dev-1",
            Platform = "android",
            FcmToken = "fcm-token",
            AppVersion = "1.0.0"
        };

        user.Vehicles.Add(vehicle);
        user.DeviceTokens.Add(device);

        user.Vehicles.Should().ContainSingle(v => v.LicensePlate == "KA01AB1" && v.IsDefault);
        user.DeviceTokens.Should().ContainSingle(d => d.Platform == "android" && d.FcmToken == "fcm-token");
    }
}
