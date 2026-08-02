using FluentAssertions;
using FluentValidation.TestHelper;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Validators;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>Wave 16: FluentValidation rules for corporate command DTOs.</summary>
public class CorporateDtoValidatorTests
{
    [Fact]
    public void CreateCompanyDto_Valid_Passes()
    {
        var validator = new CreateCompanyDtoValidator();
        var result = validator.TestValidate(new CreateCompanyDto(
            "Acme Corp", "REG-1", "a@acme.com", "9999999999", "123 Street", BillingType.UsageBased));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateCompanyDto_EmptyName_Fails()
    {
        var validator = new CreateCompanyDtoValidator();
        var result = validator.TestValidate(new CreateCompanyDto(
            "ab", "", "bad", "", "", BillingType.UsageBased));
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.RegistrationNumber);
        result.ShouldHaveValidationErrorFor(x => x.ContactEmail);
        result.ShouldHaveValidationErrorFor(x => x.ContactPhone);
        result.ShouldHaveValidationErrorFor(x => x.BillingAddress);
    }

    [Fact]
    public void InviteMemberDto_ValidAndInvalid()
    {
        var validator = new InviteMemberDtoValidator();
        validator.TestValidate(new InviteMemberDto("ok@acme.com", CompanyRole.Employee))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new InviteMemberDto("", CompanyRole.Employee))
            .ShouldHaveValidationErrorFor(x => x.Email);
        validator.TestValidate(new InviteMemberDto("not-an-email", CompanyRole.Employee))
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void AddMemberDto_PriorityOutOfRange_Fails()
    {
        var validator = new AddMemberDtoValidator();
        var result = validator.TestValidate(new AddMemberDto("a@b.com", CompanyRole.Employee, "E1", 11));
        result.ShouldHaveValidationErrorFor(x => x.Priority);
    }

    [Fact]
    public void AllocateParkingSlotsDto_SumExceedsTotal_Fails()
    {
        var validator = new AllocateParkingSlotsDtoValidator();
        var start = DateTime.UtcNow.Date;
        var result = validator.TestValidate(new AllocateParkingSlotsDto(
            Guid.NewGuid(), 5, 3, 3, 1000m, start, start.AddMonths(1), null, null));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AllocateParkingSlotsDto_Valid_Passes()
    {
        var validator = new AllocateParkingSlotsDtoValidator();
        var start = DateTime.UtcNow.Date;
        var result = validator.TestValidate(new AllocateParkingSlotsDto(
            Guid.NewGuid(), 5, 2, 3, 1000m, start, start.AddMonths(1), null, null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void AllocateParkingSlotsDto_DualPools_Valid_Passes()
    {
        var validator = new AllocateParkingSlotsDtoValidator();
        var start = DateTime.UtcNow.Date;
        var result = validator.TestValidate(new AllocateParkingSlotsDto(
            ParkingSpaceId: Guid.NewGuid(),
            MonthlyRate: 1000m,
            StartDate: start,
            EndDate: start.AddMonths(1),
            TwoWheeler: new SlotPoolDto(10, 2, 8),
            FourWheeler: new SlotPoolDto(20, 5, 15)));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void AllocateParkingSlotsDto_DualPools_FixedExceedsTotal_Fails()
    {
        var validator = new AllocateParkingSlotsDtoValidator();
        var start = DateTime.UtcNow.Date;
        var result = validator.TestValidate(new AllocateParkingSlotsDto(
            ParkingSpaceId: Guid.NewGuid(),
            MonthlyRate: 1000m,
            StartDate: start,
            EndDate: start.AddMonths(1),
            TwoWheeler: new SlotPoolDto(5, 4, 3),
            FourWheeler: new SlotPoolDto(5, 0, 5)));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void AllocateParkingSlotsDto_DualPools_BothZero_Fails()
    {
        var validator = new AllocateParkingSlotsDtoValidator();
        var start = DateTime.UtcNow.Date;
        var result = validator.TestValidate(new AllocateParkingSlotsDto(
            ParkingSpaceId: Guid.NewGuid(),
            MonthlyRate: 1000m,
            StartDate: start,
            EndDate: start.AddMonths(1),
            TwoWheeler: new SlotPoolDto(0, 0, 0),
            FourWheeler: new SlotPoolDto(0, 0, 0)));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void AssignFixedSlotDto_ValidVehicleClass_Passes()
    {
        var validator = new AssignFixedSlotDtoValidator();
        validator.TestValidate(new AssignFixedSlotDto(Guid.NewGuid(), 1, VehicleClass.TwoWheeler))
            .ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new AssignFixedSlotDto(Guid.NewGuid(), 2, VehicleClass.FourWheeler))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    [Trait("Feature", "VehicleClassPools")]
    public void AssignFixedSlotDto_InvalidVehicleClass_Fails()
    {
        var validator = new AssignFixedSlotDtoValidator();
        var result = validator.TestValidate(new AssignFixedSlotDto(Guid.NewGuid(), 1, (VehicleClass)99));
        result.ShouldHaveValidationErrorFor(x => x.VehicleClass);
    }

    [Fact]
    public void BookingPolicyDto_WeeklyLessThanDaily_Fails()
    {
        var validator = new BookingPolicyDtoValidator();
        var result = validator.TestValidate(new BookingPolicyDto(
            5, 3, 1, TimeSpan.FromHours(7), TimeSpan.FromHours(22), true));
        result.ShouldHaveValidationErrorFor(x => x.MaxBookingsPerEmployeePerWeek);
    }

    [Fact]
    public void AssignFixedSlotDto_EmptyMembership_Fails()
    {
        var validator = new AssignFixedSlotDtoValidator();
        var result = validator.TestValidate(new AssignFixedSlotDto(Guid.Empty, 0));
        result.ShouldHaveValidationErrorFor(x => x.MembershipId);
        result.ShouldHaveValidationErrorFor(x => x.SlotNumber);
    }

    [Fact]
    public void BookCorporateParkingDto_EndBeforeStart_Fails()
    {
        var validator = new BookCorporateParkingDtoValidator();
        var start = DateTime.UtcNow.AddHours(2);
        var result = validator.TestValidate(new BookCorporateParkingDto(
            Guid.NewGuid(), start, start.AddHours(-1)));
        result.ShouldHaveValidationErrorFor(x => x.EndDateTime);
    }

    [Fact]
    public void BookVisitorParkingDto_MissingNameAndExpiry_Fails()
    {
        var validator = new BookVisitorParkingDtoValidator();
        var start = DateTime.UtcNow.AddHours(2);
        var end = start.AddHours(2);
        var result = validator.TestValidate(new BookVisitorParkingDto(
            Guid.NewGuid(), start, end, "A", "X", end.AddHours(-1)));
        result.ShouldHaveValidationErrorFor(x => x.VisitorName);
        result.ShouldHaveValidationErrorFor(x => x.VisitorLicensePlate);
        result.ShouldHaveValidationErrorFor(x => x.AccessExpiry);
    }

    [Fact]
    public void BookVisitorParkingDto_Valid_Passes()
    {
        var validator = new BookVisitorParkingDtoValidator();
        var start = DateTime.UtcNow.AddHours(2);
        var end = start.AddHours(2);
        var result = validator.TestValidate(new BookVisitorParkingDto(
            Guid.NewGuid(), start, end, "Guest Name", "KA01AB1234", end));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
