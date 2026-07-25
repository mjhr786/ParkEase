using FluentAssertions;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Enums;

namespace ParkingApp.Identity.UnitTests;

public class IdentityDomainSmokeTests
{
    [Fact]
    public void User_Register_CreatesActiveUser()
    {
        var user = User.Register("ada@example.com", "hash", "Ada", "Lovelace", "555");
        user.Should().NotBeNull();
        user.IsActive.Should().BeTrue();
        user.Role.Should().Be(UserRole.User);
        user.Email.Value.Should().Be("ada@example.com");
    }
}
