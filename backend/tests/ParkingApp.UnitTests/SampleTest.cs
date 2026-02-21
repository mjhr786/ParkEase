using FluentAssertions;
using Xunit;

namespace ParkingApp.UnitTests;

public class SampleTest
{
    [Fact]
    public void InitialTest_ShouldPass()
    {
        // Arrange
        var value = 1;

        // Act
        var result = value + 1;

        // Assert
        result.Should().Be(2);
    }
}
