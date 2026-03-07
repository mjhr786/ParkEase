using FluentAssertions;
using Xunit;
using ParkingApp.Domain.ValueObjects;
using System;

namespace ParkingApp.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithValidValues_ShouldCreateInstance()
    {
        var money = new Money(100.50m, "usd");
        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_WithNegativeAmount_ShouldThrowArgumentException()
    {
        var act = () => new Money(-1m, "USD");
        act.Should().Throw<ArgumentException>().WithMessage("Amount cannot be negative*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithEmptyCurrency_ShouldThrowArgumentException(string? invalidCurrency)
    {
        var act = () => new Money(100m, invalidCurrency);
        act.Should().Throw<ArgumentException>().WithMessage("Currency cannot be empty*");
    }

    [Fact]
    public void Zero_ShouldReturnMoneyWithZeroAmount()
    {
        var money = Money.Zero("EUR");
        money.Amount.Should().Be(0);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void AdditionOperator_WithSameCurrency_ShouldReturnSum()
    {
        var m1 = new Money(10m, "USD");
        var m2 = new Money(15m, "USD");
        var result = m1 + m2;
        result.Amount.Should().Be(25m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void AdditionOperator_WithDifferentCurrency_ShouldThrowException()
    {
        var m1 = new Money(10m, "USD");
        var m2 = new Money(15m, "EUR");
        var act = () => m1 + m2;
        act.Should().Throw<InvalidOperationException>().WithMessage("*different currencies*");
    }

    [Fact]
    public void SubtractionOperator_WithSameCurrency_ShouldReturnDifference()
    {
        var m1 = new Money(20m, "USD");
        var m2 = new Money(5m, "USD");
        var result = m1 - m2;
        result.Amount.Should().Be(15m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void SubtractionOperator_WithDifferentCurrency_ShouldThrowException()
    {
        var m1 = new Money(20m, "USD");
        var m2 = new Money(5m, "EUR");
        var act = () => m1 - m2;
        act.Should().Throw<InvalidOperationException>().WithMessage("*different currencies*");
    }

    [Fact]
    public void MultiplicationOperator_ShouldMultiplyAmount()
    {
        var money = new Money(10m, "USD");
        var result = money * 2.5m;
        result.Amount.Should().Be(25m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void GreaterThanOperator_WithSameCurrency_ShouldCompareAmounts()
    {
        var larger = new Money(20m, "USD");
        var smaller = new Money(10m, "USD");
        (larger > smaller).Should().BeTrue();
        (smaller > larger).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOperator_WithDifferentCurrency_ShouldThrowException()
    {
        var m1 = new Money(20m, "USD");
        var m2 = new Money(10m, "EUR");
        var act = () => m1 > m2;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LessThanOperator_WithSameCurrency_ShouldCompareAmounts()
    {
        var larger = new Money(20m, "USD");
        var smaller = new Money(10m, "USD");
        (smaller < larger).Should().BeTrue();
        (larger < smaller).Should().BeFalse();
    }

    [Fact]
    public void LessThanOperator_WithDifferentCurrency_ShouldThrowException()
    {
        var m1 = new Money(10m, "USD");
        var m2 = new Money(20m, "EUR");
        var act = () => m1 < m2;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GreaterThanOrEqualOperator_ShouldCompareCorrectly()
    {
        var m1 = new Money(20m, "USD");
        var m2 = new Money(20m, "USD");
        var m3 = new Money(10m, "USD");
        (m1 >= m2).Should().BeTrue();
        (m1 >= m3).Should().BeTrue();
        (m3 >= m1).Should().BeFalse();
    }

    [Fact]
    public void LessThanOrEqualOperator_ShouldCompareCorrectly()
    {
        var m1 = new Money(10m, "USD");
        var m2 = new Money(10m, "USD");
        var m3 = new Money(20m, "USD");
        (m1 <= m2).Should().BeTrue();
        (m1 <= m3).Should().BeTrue();
        (m3 <= m1).Should().BeFalse();
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var money = new Money(15.5m, "usd");
        money.ToString().Should().Be("USD 15.50");
    }
}
