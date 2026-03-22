using FluentAssertions;
using Xunit;
using ParkingApp.Domain.ValueObjects;
using System;

namespace ParkingApp.UnitTests.Domain;

public class EmailTests
{
    [Theory]
    [InlineData("test@example.com")]
    [InlineData("TEST@EXAMPLE.COM")]
    [InlineData("user.name+tag@domain.co.uk")]
    [InlineData("user_name123@sub.domain.org")]
    public void Constructor_WithValidEmail_ShouldNormalizeAndCreate(string validEmail)
    {
        var email = new Email(validEmail);
        email.Value.Should().Be(validEmail.Trim().ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithEmptyEmail_ShouldThrowArgumentException(string? invalidEmail)
    {
        var act = () => new Email(invalidEmail);
        act.Should().Throw<ArgumentException>().WithMessage("Email cannot be empty*");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("test@")]
    [InlineData("@example.com")]
    [InlineData("test@example")]
    public void Constructor_WithInvalidFormat_ShouldThrowArgumentException(string invalidEmail)
    {
        var act = () => new Email(invalidEmail);
        act.Should().Throw<ArgumentException>().WithMessage("Invalid email format*");
    }

    [Fact]
    public void ImplicitOperatorString_ShouldReturnEmailValue()
    {
        var email = new Email("test@example.com");
        string stringValue = email;
        stringValue.Should().Be("test@example.com");
    }

    [Fact]
    public void ExplicitOperatorEmail_ShouldCreateEmailInstance()
    {
        var stringValue = "test@example.com";
        var email = (Email)stringValue;
        email.Should().NotBeNull();
        email.Value.Should().Be(stringValue);
    }

    [Fact]
    public void ToString_ShouldReturnEmailValue()
    {
        var email = new Email("test@example.com");
        email.ToString().Should().Be("test@example.com");
    }
}
