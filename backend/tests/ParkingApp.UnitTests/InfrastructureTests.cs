using FluentAssertions;
using Xunit;
using ParkingApp.BuildingBlocks.Common;
using System;

namespace ParkingApp.UnitTests;

public class InfrastructureTests
{
    [Fact]
    public void Guard_AgainstNull_ShouldThrowWhenNull()
    {
        string? nullString = null;
        Action act = () => Guard.AgainstNull(nullString, "testParam");
        act.Should().Throw<ArgumentNullException>().WithMessage("*cannot be null*");
    }

    [Fact]
    public void Guard_AgainstNegative_ShouldThrowWhenNegative()
    {
        Action act = () => Guard.AgainstNegative(-1, "amount");
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*cannot be negative*");
    }

    [Fact]
    public void Guard_AgainstInvalidEmail_ShouldThrowForBadEmail()
    {
        Action act = () => Guard.AgainstInvalidEmail("not-an-email", "email");
        act.Should().Throw<ArgumentException>().WithMessage("*not a valid email address*");
    }

    [Fact]
    public void Guard_AgainstEmpty_ShouldThrowForEmptyGuid()
    {
        Action act = () => Guard.AgainstEmpty(Guid.Empty, "id");
        act.Should().Throw<ArgumentException>().WithMessage("*cannot be an empty GUID*");
    }
}
