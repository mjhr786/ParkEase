using FluentValidation;
using ParkingApp.Marketplace.Application.Commands.Lpr;
using ParkingApp.Marketplace.Domain.ValueObjects;

namespace ParkingApp.Marketplace.Application.Validators;

internal sealed class ProcessLprAccessCommandValidator : AbstractValidator<ProcessLprAccessCommand>
{
    public ProcessLprAccessCommandValidator()
    {
        RuleFor(x => x.LicensePlate)
            .NotEmpty().WithMessage("License plate is required")
            .MaximumLength(LicensePlate.MaxLength + 10)
            .WithMessage($"License plate must not exceed {LicensePlate.MaxLength} characters after normalization");

        RuleFor(x => x.ParkingSpaceId)
            .NotEmpty().WithMessage("Parking space is required");

        RuleFor(x => x.Direction)
            .IsInEnum().WithMessage("Direction must be Entry or Exit");

        RuleFor(x => x.Source)
            .NotEmpty().WithMessage("Source is required")
            .MaximumLength(32);

        RuleFor(x => x.OccurredAtUtc)
            .Must(t => !t.HasValue || t.Value <= DateTime.UtcNow.AddMinutes(5))
            .WithMessage("OccurredAtUtc cannot be more than 5 minutes in the future")
            .Must(t => !t.HasValue || t.Value >= DateTime.UtcNow.AddHours(-24))
            .WithMessage("OccurredAtUtc cannot be older than 24 hours");
    }
}
