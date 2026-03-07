using System;
using System.Collections.Generic;
using FluentAssertions;
using ParkingApp.BuildingBlocks.Common;
using Xunit;

namespace ParkingApp.UnitTests.BuildingBlocks;

public class GuardTests
{
    [Fact]
    public void AgainstNull_ShouldReturnObject_WhenNotNull()
    {
        var obj = new object();
        var result = Guard.AgainstNull(obj, nameof(obj));
        result.Should().Be(obj);
    }

    [Fact]
    public void AgainstNull_ShouldThrow_WhenNull()
    {
        object? obj = null;
        Action act = () => Guard.AgainstNull(obj, nameof(obj));
        act.Should().Throw<ArgumentNullException>().WithParameterName(nameof(obj));
    }

    [Fact]
    public void AgainstNullOrEmpty_ShouldReturnString_WhenValid()
    {
        var result = Guard.AgainstNullOrEmpty("valid", "param");
        result.Should().Be("valid");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void AgainstNullOrEmpty_ShouldThrow_WhenInvalid(string? value)
    {
        Action act = () => Guard.AgainstNullOrEmpty(value, "param");
        act.Should().Throw<ArgumentException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNullOrWhiteSpace_ShouldReturnString_WhenValid()
    {
        var result = Guard.AgainstNullOrWhiteSpace("valid", "param");
        result.Should().Be("valid");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void AgainstNullOrWhiteSpace_ShouldThrow_WhenInvalid(string? value)
    {
        Action act = () => Guard.AgainstNullOrWhiteSpace(value, "param");
        act.Should().Throw<ArgumentException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstEmpty_ShouldReturnGuid_WhenNotEmpty()
    {
        var guid = Guid.NewGuid();
        var result = Guard.AgainstEmpty(guid, "param");
        result.Should().Be(guid);
    }

    [Fact]
    public void AgainstEmpty_ShouldThrow_WhenEmptyGuid()
    {
        Action act = () => Guard.AgainstEmpty(Guid.Empty, "param");
        act.Should().Throw<ArgumentException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstDefault_ShouldReturnStruct_WhenNotDefault()
    {
        var result = Guard.AgainstDefault(1, "param");
        result.Should().Be(1);
    }

    [Fact]
    public void AgainstDefault_ShouldThrow_WhenDefaultStruct()
    {
        Action act = () => Guard.AgainstDefault(0, "param");
        act.Should().Throw<ArgumentException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNegative_Int_ShouldReturn_WhenZeroOrPositive()
    {
        Guard.AgainstNegative(0, "param").Should().Be(0);
        Guard.AgainstNegative(1, "param").Should().Be(1);
    }

    [Fact]
    public void AgainstNegative_Int_ShouldThrow_WhenNegative()
    {
        Action act = () => Guard.AgainstNegative(-1, "param");
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNegative_Decimal_ShouldReturn_WhenZeroOrPositive()
    {
        Guard.AgainstNegative(0m, "param").Should().Be(0m);
        Guard.AgainstNegative(1m, "param").Should().Be(1m);
    }

    [Fact]
    public void AgainstNegative_Decimal_ShouldThrow_WhenNegative()
    {
        Action act = () => Guard.AgainstNegative(-1m, "param");
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNegativeOrZero_Int_ShouldReturn_WhenPositive()
    {
        Guard.AgainstNegativeOrZero(1, "param").Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AgainstNegativeOrZero_Int_ShouldThrow_WhenZeroOrNegative(int value)
    {
        Action act = () => Guard.AgainstNegativeOrZero(value, "param");
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNegativeOrZero_Decimal_ShouldReturn_WhenPositive()
    {
        Guard.AgainstNegativeOrZero(1m, "param").Should().Be(1m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AgainstNegativeOrZero_Decimal_ShouldThrow_WhenZeroOrNegative(decimal value)
    {
        Action act = () => Guard.AgainstNegativeOrZero(value, "param");
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstOutOfRange_ShouldReturn_WhenInRange()
    {
        Guard.AgainstOutOfRange(5, "param", 1, 10).Should().Be(5);
        Guard.AgainstOutOfRange(1, "param", 1, 10).Should().Be(1);
        Guard.AgainstOutOfRange(10, "param", 1, 10).Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void AgainstOutOfRange_ShouldThrow_WhenOutOfRange(int value)
    {
        Action act = () => Guard.AgainstOutOfRange(value, "param", 1, 10);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNullOrEmpty_Enumerable_ShouldReturn_WhenNotEmpty()
    {
        var list = new List<int> { 1, 2 };
        Guard.AgainstNullOrEmpty(list, "param").Should().BeEquivalentTo(list);
    }

    [Fact]
    public void AgainstNullOrEmpty_Enumerable_ShouldThrow_WhenEmpty()
    {
        var list = new List<int>();
        Action act = () => Guard.AgainstNullOrEmpty(list, "param");
        act.Should().Throw<ArgumentException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstNullOrEmpty_Enumerable_ShouldThrow_WhenNull()
    {
        List<int>? list = null;
        Action act = () => Guard.AgainstNullOrEmpty(list, "param");
        act.Should().Throw<ArgumentException>().WithParameterName("param");
    }

    [Theory]
    [InlineData("test@test.com")]
    [InlineData("a.b@c.com")]
    public void AgainstInvalidEmail_ShouldReturn_WhenValid(string email)
    {
        Guard.AgainstInvalidEmail(email, "param").Should().Be(email);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("test@test")]
    [InlineData("test.com")]
    public void AgainstInvalidEmail_ShouldThrow_WhenInvalid(string email)
    {
        Action act = () => Guard.AgainstInvalidEmail(email, "param");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AgainstPast_ShouldReturn_WhenFuture()
    {
        var future = DateTime.UtcNow.AddHours(1);
        Guard.AgainstPast(future, "param").Should().Be(future);
    }

    [Fact]
    public void AgainstPast_ShouldThrow_WhenPast()
    {
        var past = DateTime.UtcNow.AddHours(-1);
        Action act = () => Guard.AgainstPast(past, "param");
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("param");
    }

    [Fact]
    public void AgainstFuture_ShouldReturn_WhenPast()
    {
        var past = DateTime.UtcNow.AddHours(-1);
        Guard.AgainstFuture(past, "param").Should().Be(past);
    }

    [Fact]
    public void AgainstFuture_ShouldThrow_WhenFuture()
    {
        var future = DateTime.UtcNow.AddHours(1);
        Action act = () => Guard.AgainstFuture(future, "param");
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("param");
    }
}
