using System;
using System.Collections.Generic;
using FluentValidation.TestHelper;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Validators;
using ParkingApp.Domain.Enums;
using Xunit;

namespace ParkingApp.UnitTests.Validators;

public class DtoValidatorTests
{
    private readonly RegisterDtoValidator _registerValidator = new();
    private readonly LoginDtoValidator _loginValidator = new();
    private readonly CreateParkingSpaceDtoValidator _createParkingValidator = new();
    private readonly CreateBookingDtoValidator _createBookingValidator = new();
    private readonly CreateReviewDtoValidator _createReviewValidator = new();

    // RegisterDtoValidator Tests
    [Fact]
    public void RegisterDtoValidator_ShouldHaveError_WhenEmailInvalid()
    {
        var dto = new RegisterDto("invalid-email", "Pa$$w0rd", "First", "Last", "1234567890", UserRole.Member);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void RegisterDtoValidator_ShouldHaveError_WhenPasswordWeak()
    {
        var dto = new RegisterDto("test@test.com", "weak", "First", "Last", "1234567890", UserRole.Member);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void RegisterDtoValidator_ShouldNotHaveError_WhenValid()
    {
        var dto = new RegisterDto("test@test.com", "Str0ngP@ssw0rd!", "First", "Last", "1234567890", UserRole.Member);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    // LoginDtoValidator Tests
    [Fact]
    public void LoginDtoValidator_ShouldHaveError_WhenEmailOrPasswordMissing()
    {
        var dto = new LoginDto("", "");
        var result = _loginValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void LoginDtoValidator_ShouldNotHaveError_WhenValid()
    {
        var dto = new LoginDto("test@test.com", "password");
        var result = _loginValidator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // CreateParkingSpaceDtoValidator Tests
    [Fact]
    public void CreateParkingSpaceDtoValidator_ShouldHaveError_WhenTitleMissing()
    {
        var dto = new CreateParkingSpaceDto("", "Desc", "Addr", "City", "State", "Country", "P", 1, 1, ParkingType.Open, 1, 1, 1, 1, 1, null, null, false, null, null, null, null);
        var result = _createParkingValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void CreateParkingSpaceDtoValidator_ShouldHaveError_WhenSpotsInvalid()
    {
        var dto = new CreateParkingSpaceDto("T", "D", "A", "C", "S", "Co", "P", 1, 1, ParkingType.Open, 0, 1, 1, 1, 1, null, null, false, null, null, null, null);
        var result = _createParkingValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.TotalSpots);
    }

    [Fact]
    public void CreateParkingSpaceDtoValidator_ShouldNotHaveError_WhenValid()
    {
        var dto = new CreateParkingSpaceDto("T", "D", "A", "C", "S", "Co", "P", 1, 1, ParkingType.Open, 1, 1, 1, 1, 1, null, null, false, null, null, null, null);
        var result = _createParkingValidator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // CreateBookingDtoValidator Tests
    [Fact]
    public void CreateBookingDtoValidator_ShouldHaveError_WhenDatesInvalid()
    {
        var dto = new CreateBookingDto(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-2), PricingType.Hourly, VehicleType.Car, null, null, null, null, null);
        var result = _createBookingValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.StartDateTime);
        result.ShouldHaveValidationErrorFor(x => x.EndDateTime);
    }

    [Fact]
    public void CreateBookingDtoValidator_ShouldNotHaveError_WhenValid()
    {
        var dto = new CreateBookingDto(Guid.NewGuid(), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), PricingType.Hourly, VehicleType.Car, null, null, null, null, null);
        var result = _createBookingValidator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // CreateReviewDtoValidator Tests
    [Fact]
    public void CreateReviewDtoValidator_ShouldHaveError_WhenRatingInvalid()
    {
        var dto = new CreateReviewDto(Guid.NewGuid(), null, 6, "Title", "Comment");
        var result = _createReviewValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Rating);
    }

    [Fact]
    public void CreateReviewDtoValidator_ShouldNotHaveError_WhenValid()
    {
        var dto = new CreateReviewDto(Guid.NewGuid(), null, 5, "Title", "Comment");
        var result = _createReviewValidator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
