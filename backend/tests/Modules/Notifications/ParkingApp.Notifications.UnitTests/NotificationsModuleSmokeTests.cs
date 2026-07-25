using FluentAssertions;
using ParkingApp.Notifications.Contracts;

namespace ParkingApp.Notifications.UnitTests;

public class NotificationsModuleSmokeTests
{
    [Fact]
    public void Contracts_Assembly_Loads()
    {
        var asm = typeof(INotificationService).Assembly;
        asm.GetName().Name.Should().Be("ParkingApp.Notifications.Contracts");
    }
}
