using FluentValidation;
using ParkingApp.Application.CQRS.Commands.Bookings;

namespace ParkingApp.Application.Validators;

/// <summary>
/// Command-level rules for create booking (pipeline validation; mirrors CreateBookingDto rules).
/// </summary>
public sealed class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
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
