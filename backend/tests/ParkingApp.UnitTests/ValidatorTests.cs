using FluentAssertions;
using Xunit;
using ParkingApp.Application.Validators;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests;

public class ValidatorTests
{
    private readonly RegisterDtoValidator _registerValidator = new();
    private readonly CreateParkingSpaceDtoValidator _parkingValidator = new();

    [Theory]
    [InlineData("weak", false)] // Too short
    [InlineData("NoSpecial123", false)] // No special char
    [InlineData("nospecial!", false)] // No uppercase
    [InlineData("NOSPECIAL!", false)] // No lowercase
    [InlineData("Valid123!", true)] // Strong
    public void RegisterValidator_PasswordComplexity_ShouldValidateCorrectly(string password, bool expectedValid)
    {
        var dto = new RegisterDto("test@test.com", password, "John", "Doe", "+1234567890", UserRole.Member);
        var result = _registerValidator.Validate(dto);

        result.IsValid.Should().Be(expectedValid);
        if (!expectedValid)
        {
            result.Errors.Should().Contain(e => e.PropertyName == "Password");
        }
    }

    [Theory]
    [InlineData("invalid-email", false)]
    [InlineData("test@domain.com", true)]
    public void RegisterValidator_EmailFormat_ShouldValidateCorrectly(string email, bool expectedValid)
    {
        var dto = new RegisterDto(email, "StrongPass123!", "John", "Doe", "+1234567890", UserRole.Member);
        var result = _registerValidator.Validate(dto);

        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData("123", false)] // Too short
    [InlineData("+1234567890", true)] // Valid E.164
    [InlineData("1234567890123456", false)] // Too long
    public void RegisterValidator_PhoneNumber_ShouldValidateCorrectly(string phone, bool expectedValid)
    {
        var dto = new RegisterDto("test@test.com", "StrongPass123!", "John", "Doe", phone, UserRole.Member);
        var result = _registerValidator.Validate(dto);

        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void CreateParkingSpaceValidator_Coordinates_ShouldBeInRange()
    {
        var invalidDto = new CreateParkingSpaceDto(
            "Title", "Desc", "Addr", "City", "ST", "IN", "123",
            100.0, 200.0, // Invalid lat/lng
            ParkingType.Open, 10, 50, 400, 2000, 7000, null, null);

        var result = _parkingValidator.Validate(invalidDto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Latitude");
        result.Errors.Should().Contain(e => e.PropertyName == "Longitude");
    }

    [Fact]
    public void CreateParkingSpaceValidator_Rates_ShouldNotBeNegative()
    {
        var invalidDto = new CreateParkingSpaceDto(
            "Title", "Desc", "Addr", "City", "ST", "IN", "123",
            12.0, 77.0, ParkingType.Open, 10, 
            -10, 400, 2000, 7000, null, null); // Negative hourly rate

        var result = _parkingValidator.Validate(invalidDto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "HourlyRate");
    }
}
