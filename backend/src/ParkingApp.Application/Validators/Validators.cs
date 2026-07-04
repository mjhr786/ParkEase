using FluentValidation;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.Validators;

public class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z\s'-]+$").WithMessage("First name contains invalid characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z\s'-]+$").WithMessage("Last name contains invalid characters");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .Matches(@"^\+?[1-9]\d{9,14}$").WithMessage("Invalid phone number format");
    }
}

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}

public class CreateParkingSpaceDtoValidator : AbstractValidator<CreateParkingSpaceDto>
{
    public CreateParkingSpaceDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required")
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters");

        RuleFor(x => x.State)
            .NotEmpty().WithMessage("State is required")
            .MaximumLength(100).WithMessage("State must not exceed 100 characters");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters");

        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("Postal code is required")
            .MaximumLength(20).WithMessage("Postal code must not exceed 20 characters");

        RuleFor(x => x.ZoneCode)
            .MaximumLength(64).WithMessage("Zone code must not exceed 64 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ZoneCode));

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180");

        RuleFor(x => x.TotalSpots)
            .GreaterThan(0).WithMessage("Total spots must be greater than 0")
            .LessThanOrEqualTo(1000).WithMessage("Total spots must not exceed 1000");

        RuleFor(x => x.HourlyRate)
            .GreaterThanOrEqualTo(0).WithMessage("Hourly rate cannot be negative");

        RuleFor(x => x.DailyRate)
            .GreaterThanOrEqualTo(0).WithMessage("Daily rate cannot be negative");

        RuleFor(x => x.WeeklyRate)
            .GreaterThanOrEqualTo(0).WithMessage("Weekly rate cannot be negative");

        RuleFor(x => x.MonthlyRate)
            .GreaterThanOrEqualTo(0).WithMessage("Monthly rate cannot be negative");
    }
}

public class CreateBookingDtoValidator : AbstractValidator<CreateBookingDto>
{
    public CreateBookingDtoValidator()
    {
        RuleFor(x => x.ParkingSpaceId)
            .NotEmpty().WithMessage("Parking space is required");

        RuleFor(x => x.StartDateTime)
            .NotEmpty().WithMessage("Start date/time is required")
            .GreaterThan(DateTime.UtcNow).WithMessage("Start date/time must be in the future");

        RuleFor(x => x.EndDateTime)
            .NotEmpty().WithMessage("End date/time is required")
            .GreaterThan(x => x.StartDateTime).WithMessage("End date/time must be after start date/time");

        RuleFor(x => x.VehicleNumber)
            .MaximumLength(20).WithMessage("Vehicle number must not exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.VehicleNumber));

        RuleFor(x => x.VehicleModel)
            .MaximumLength(100).WithMessage("Vehicle model must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.VehicleModel));
    }
}

public class CreateReviewDtoValidator : AbstractValidator<CreateReviewDto>
{
    public CreateReviewDtoValidator()
    {
        RuleFor(x => x.ParkingSpaceId)
            .NotEmpty().WithMessage("Parking space is required");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5");

        RuleFor(x => x.Title)
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Comment)
            .MaximumLength(2000).WithMessage("Comment must not exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Comment));
    }
}

public class CreateParkingPassDtoValidator : AbstractValidator<CreateParkingPassDto>
{
    public CreateParkingPassDtoValidator()
    {
        RuleFor(x => x.PassType)
            .IsInEnum().WithMessage("A valid parking pass type is required.")
            .NotEqual(PassTypeKind.Corporate).WithMessage("Corporate passes must be created through the corporate endpoint.");

        RuleFor(x => x.StartDateUtc)
            .GreaterThan(DateTime.UtcNow.AddMinutes(-1)).WithMessage("Pass start date must not be in the past.");

        RuleFor(x => x.EndDateUtc)
            .GreaterThan(x => x.StartDateUtc).WithMessage("Pass end date must be after the start date.");

        RuleFor(x => x.DiscountPercentage)
            .InclusiveBetween(0, 100).WithMessage("Discount percentage must be between 0 and 100.");

        RuleFor(x => x.ParkingSpaceId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty).WithMessage("Parking space is required.")
            .Must((dto, parkingSpaceId) => parkingSpaceId.HasValue ^ !string.IsNullOrWhiteSpace(dto.ParkingZoneCode))
            .WithMessage("Select either a parking space or a parking zone.");

        RuleFor(x => x.ParkingZoneCode)
            .MaximumLength(64).WithMessage("Parking zone code must not exceed 64 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.ParkingZoneCode));

        RuleFor(x => x.UsageMode)
            .IsInEnum().WithMessage("A valid pass usage mode is required.");

        RuleFor(x => x.DailyHourLimit)
            .Null().WithMessage("Unlimited entry passes cannot define a daily hour limit.")
            .When(x => x.UsageMode == PassUsageMode.UnlimitedEntries);

        RuleFor(x => x.DailyHourLimit)
            .InclusiveBetween(1, 24).WithMessage("Daily hour limit must be between 1 and 24.")
            .When(x => x.UsageMode == PassUsageMode.LimitedHoursPerDay);
    }
}

public class AssignCorporatePassDtoValidator : AbstractValidator<AssignCorporatePassDto>
{
    public AssignCorporatePassDtoValidator()
    {
        RuleFor(x => x.EmployeeUserIds)
            .NotNull().WithMessage("At least one employee is required.")
            .Must(ids => ids != null && ids.Any()).WithMessage("At least one employee is required.");

        RuleForEach(x => x.EmployeeUserIds)
            .NotEmpty().WithMessage("Employee user id is required.");

        RuleFor(x => x.StartDateUtc)
            .GreaterThan(DateTime.UtcNow.AddMinutes(-1)).WithMessage("Pass start date must not be in the past.");

        RuleFor(x => x.EndDateUtc)
            .GreaterThan(x => x.StartDateUtc).WithMessage("Pass end date must be after the start date.");

        RuleFor(x => x.DiscountPercentage)
            .InclusiveBetween(0, 100).WithMessage("Discount percentage must be between 0 and 100.");

        RuleFor(x => x.ParkingSpaceId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty).WithMessage("Parking space is required.")
            .Must((dto, parkingSpaceId) => parkingSpaceId.HasValue ^ !string.IsNullOrWhiteSpace(dto.ParkingZoneCode))
            .WithMessage("Select either a parking space or a parking zone.");

        RuleFor(x => x.ParkingZoneCode)
            .MaximumLength(64).WithMessage("Parking zone code must not exceed 64 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.ParkingZoneCode));

        RuleFor(x => x.UsageMode)
            .IsInEnum().WithMessage("A valid pass usage mode is required.");

        RuleFor(x => x.DailyHourLimit)
            .Null().WithMessage("Unlimited entry passes cannot define a daily hour limit.")
            .When(x => x.UsageMode == PassUsageMode.UnlimitedEntries);

        RuleFor(x => x.DailyHourLimit)
            .InclusiveBetween(1, 24).WithMessage("Daily hour limit must be between 1 and 24.")
            .When(x => x.UsageMode == PassUsageMode.LimitedHoursPerDay);
    }
}
