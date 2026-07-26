using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Corporate.Application;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>
/// Regression for DEF-003: PassesController required IValidator&lt;AssignCorporatePassDto&gt;
/// but Corporate DI never registered it, which broke the entire /api/passes controller.
/// </summary>
public class CorporateApplicationModuleTests
{
    [Fact]
    public void AddCorporateApplication_RegistersAssignCorporatePassDtoValidator()
    {
        var services = new ServiceCollection();
        services.AddCorporateApplication();
        using var sp = services.BuildServiceProvider();

        var validator = sp.GetService<IValidator<AssignCorporatePassDto>>();
        validator.Should().NotBeNull();
    }

    [Fact]
    public async Task AssignCorporatePassDtoValidator_RejectsEmptyEmployeeList()
    {
        var services = new ServiceCollection();
        services.AddCorporateApplication();
        using var sp = services.BuildServiceProvider();
        var validator = sp.GetRequiredService<IValidator<AssignCorporatePassDto>>();

        var dto = new AssignCorporatePassDto(
            EmployeeUserIds: Array.Empty<Guid>(),
            StartDateUtc: DateTime.UtcNow.AddHours(1),
            EndDateUtc: DateTime.UtcNow.AddDays(30),
            DiscountPercentage: 10,
            ParkingSpaceId: Guid.NewGuid(),
            ParkingZoneCode: null,
            UsageMode: PassUsageMode.UnlimitedEntries,
            DailyHourLimit: null);

        var result = await validator.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
    }
}
