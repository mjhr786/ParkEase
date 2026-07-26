using System;
using System.Linq;
using FluentValidation;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Corporate.Application.Validators;

/// <summary>
/// Validates admin corporate pass assignment. Registered in <see cref="CorporateApplicationModule"/>.
/// </summary>
internal class AssignCorporatePassDtoValidator : AbstractValidator<AssignCorporatePassDto>
{
    public AssignCorporatePassDtoValidator()
    {
        RuleFor(x => x.EmployeeUserIds).NotNull().Must(ids => ids != null && ids.Any());
        RuleForEach(x => x.EmployeeUserIds).NotEmpty();
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

