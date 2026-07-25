using System;
using FluentValidation;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Application.Validators;

internal class CreateParkingSpaceDtoValidator : AbstractValidator<CreateParkingSpaceDto>
{
    public CreateParkingSpaceDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ZoneCode).MaximumLength(64).When(x => !string.IsNullOrWhiteSpace(x.ZoneCode));
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.TotalSpots).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.HourlyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DailyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WeeklyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MonthlyRate).GreaterThanOrEqualTo(0);
    }
}

internal class CreateBookingDtoValidator : AbstractValidator<CreateBookingDto>
{
    public CreateBookingDtoValidator()
    {
        RuleFor(x => x.ParkingSpaceId).NotEmpty();
        RuleFor(x => x.StartDateTime).NotEmpty().GreaterThan(DateTime.UtcNow);
        RuleFor(x => x.EndDateTime).NotEmpty().GreaterThan(x => x.StartDateTime);
        RuleFor(x => x.VehicleNumber).MaximumLength(20).When(x => !string.IsNullOrEmpty(x.VehicleNumber));
        RuleFor(x => x.VehicleModel).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.VehicleModel));
    }
}

internal class CreateReviewDtoValidator : AbstractValidator<CreateReviewDto>
{
    public CreateReviewDtoValidator()
    {
        RuleFor(x => x.ParkingSpaceId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Title).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Title));
        RuleFor(x => x.Comment).MaximumLength(2000).When(x => !string.IsNullOrEmpty(x.Comment));
    }
}

internal class CreateParkingPassDtoValidator : AbstractValidator<CreateParkingPassDto>
{
    public CreateParkingPassDtoValidator()
    {
        RuleFor(x => x.PassType).IsInEnum().NotEqual(PassTypeKind.Corporate);
        RuleFor(x => x.StartDateUtc).GreaterThan(DateTime.UtcNow.AddMinutes(-1));
        RuleFor(x => x.EndDateUtc).GreaterThan(x => x.StartDateUtc);
        RuleFor(x => x.DiscountPercentage).InclusiveBetween(0, 100);
        RuleFor(x => x.ParkingSpaceId).Must(id => !id.HasValue || id.Value != Guid.Empty).Must((dto, parkingSpaceId) => parkingSpaceId.HasValue ^ !string.IsNullOrWhiteSpace(dto.ParkingZoneCode));
        RuleFor(x => x.ParkingZoneCode).MaximumLength(64).When(x => !string.IsNullOrWhiteSpace(x.ParkingZoneCode));
        RuleFor(x => x.UsageMode).IsInEnum();
        RuleFor(x => x.DailyHourLimit).Null().When(x => x.UsageMode == PassUsageMode.UnlimitedEntries);
        RuleFor(x => x.DailyHourLimit).InclusiveBetween(1, 24).When(x => x.UsageMode == PassUsageMode.LimitedHoursPerDay);
    }
}

