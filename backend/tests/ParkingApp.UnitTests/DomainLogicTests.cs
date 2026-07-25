using FluentAssertions;
using Xunit;
using ParkingApp.BuildingBlocks.ValueObjects;
using MarketplaceMoney = ParkingApp.Marketplace.Domain.ValueObjects.Money;

namespace ParkingApp.UnitTests;

public class DomainLogicTests
{
    [Fact]
    public void Money_Operations_ShouldBeCorrect()
    {
        var m1 = new MarketplaceMoney(100);
        var m2 = new MarketplaceMoney(50);

        (m1 + m2).Amount.Should().Be(150);
        (m1 - m2).Amount.Should().Be(50);
        (m1 * 2).Amount.Should().Be(200);

        (m1 > m2).Should().BeTrue();
        (m1 < m2).Should().BeFalse();
        (m1 >= m2).Should().BeTrue();

        FluentActions.Invoking(() => new MarketplaceMoney(-10))
            .Should().Throw<ArgumentException>();

        var usd = new MarketplaceMoney(10, "USD");
        FluentActions.Invoking(() => m1 + usd)
            .Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("TEST@EXAMPLE.COM", true)]
    [InlineData("invalid-email", false)]
    [InlineData("", false)]
    public void Email_Validation_ShouldCorrectlyVerifyFormat(string emailValue, bool expectedValid)
    {
        if (expectedValid)
        {
            var email = new Email(emailValue);
            email.Value.Should().Be(emailValue.ToLowerInvariant());
        }
        else
        {
            FluentActions.Invoking(() => new Email(emailValue))
                .Should().Throw<ArgumentException>();
        }
    }
}





