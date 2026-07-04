using FluentValidation;
using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.Validators.Corporate;

public class CreateCompanyDtoValidator : AbstractValidator<CreateCompanyDto>
{
    public CreateCompanyDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .Length(3, 200).WithMessage("Company name must be between 3 and 200 characters.");

        RuleFor(x => x.RegistrationNumber)
            .NotEmpty().WithMessage("Registration number is required.")
            .MaximumLength(100).WithMessage("Registration number cannot exceed 100 characters.");

        RuleFor(x => x.ContactEmail)
            .NotEmpty().WithMessage("Contact email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithMessage("Contact phone is required.")
            .MaximumLength(20).WithMessage("Phone number cannot exceed 20 characters.");

        RuleFor(x => x.BillingAddress)
            .NotEmpty().WithMessage("Billing address is required.")
            .MaximumLength(500).WithMessage("Billing address cannot exceed 500 characters.");

        RuleFor(x => x.BillingType)
            .IsInEnum().WithMessage("Invalid billing type.");
    }
}

public class AddMemberDtoValidator : AbstractValidator<AddMemberDto>
{
    public AddMemberDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid company role.");

        RuleFor(x => x.EmployeeCode)
            .MaximumLength(50).WithMessage("Employee code cannot exceed 50 characters.");

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 10).WithMessage("Priority must be between 1 and 10.");
    }
}

public class InviteMemberDtoValidator : AbstractValidator<InviteMemberDto>
{
    public InviteMemberDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid company role.");
    }
}

public class AllocateParkingSlotsDtoValidator : AbstractValidator<AllocateParkingSlotsDto>
{
    public AllocateParkingSlotsDtoValidator()
    {
        RuleFor(x => x.ParkingSpaceId)
            .NotEmpty().WithMessage("Parking space ID is required.");

        RuleFor(x => x.TotalSlots)
            .GreaterThan(0).WithMessage("Total slots must be greater than 0.")
            .LessThanOrEqualTo(1000).WithMessage("Total slots cannot exceed 1000.");

        RuleFor(x => x.FixedSlots)
            .GreaterThanOrEqualTo(0).WithMessage("Fixed slots cannot be negative.");

        RuleFor(x => x.SharedSlots)
            .GreaterThanOrEqualTo(0).WithMessage("Shared slots cannot be negative.");

        RuleFor(x => x)
            .Must(x => x.FixedSlots + x.SharedSlots <= x.TotalSlots)
            .WithMessage("Sum of fixed and shared slots cannot exceed total slots.");

        RuleFor(x => x.MonthlyRate)
            .GreaterThanOrEqualTo(0).WithMessage("Monthly rate cannot be negative.");

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Start date is required.");

        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("End date is required.")
            .GreaterThan(x => x.StartDate).WithMessage("End date must be after start date.");
    }
}

public class BookingPolicyDtoValidator : AbstractValidator<BookingPolicyDto>
{
    public BookingPolicyDtoValidator()
    {
        RuleFor(x => x.MaxBookingsPerEmployeePerDay)
            .GreaterThan(0).WithMessage("Must allow at least 1 booking per day.");

        RuleFor(x => x.MaxBookingsPerEmployeePerWeek)
            .GreaterThanOrEqualTo(x => x.MaxBookingsPerEmployeePerDay)
            .WithMessage("Weekly limit must be greater than or equal to daily limit.");

        RuleFor(x => x.PriorityThreshold)
            .InclusiveBetween(1, 10).WithMessage("Priority threshold must be between 1 and 10.");

        RuleFor(x => x.AllowedEndTime)
            .GreaterThan(x => x.AllowedStartTime)
            .When(x => x.AllowedStartTime.HasValue && x.AllowedEndTime.HasValue)
            .WithMessage("Allowed end time must be after allowed start time.");
    }
}

public class AssignFixedSlotDtoValidator : AbstractValidator<AssignFixedSlotDto>
{
    public AssignFixedSlotDtoValidator()
    {
        RuleFor(x => x.MembershipId)
            .NotEmpty().WithMessage("Membership ID is required.");

        RuleFor(x => x.SlotNumber)
            .GreaterThan(0).WithMessage("Slot number must be greater than 0.");
    }
}

public class BookCorporateParkingDtoValidator : AbstractValidator<BookCorporateParkingDto>
{
    public BookCorporateParkingDtoValidator()
    {
        RuleFor(x => x.AllocationId)
            .NotEmpty().WithMessage("Allocation ID is required.");

        RuleFor(x => x.StartDateTime)
            .NotEmpty().WithMessage("Start time is required.")
            .GreaterThan(DateTime.UtcNow.AddMinutes(-5)).WithMessage("Start time cannot be in the past.");

        RuleFor(x => x.EndDateTime)
            .NotEmpty().WithMessage("End time is required.")
            .GreaterThan(x => x.StartDateTime).WithMessage("End time must be after start time.");
    }
}

public class BookVisitorParkingDtoValidator : AbstractValidator<BookVisitorParkingDto>
{
    public BookVisitorParkingDtoValidator()
    {
        RuleFor(x => x.AllocationId)
            .NotEmpty().WithMessage("Allocation ID is required.");

        RuleFor(x => x.StartDateTime)
            .NotEmpty().WithMessage("Start time is required.")
            .GreaterThan(DateTime.UtcNow.AddMinutes(-5)).WithMessage("Start time cannot be in the past.");

        RuleFor(x => x.EndDateTime)
            .NotEmpty().WithMessage("End time is required.")
            .GreaterThan(x => x.StartDateTime).WithMessage("End time must be after start time.");

        RuleFor(x => x.VisitorName)
            .NotEmpty().WithMessage("Visitor name is required.")
            .Length(2, 200).WithMessage("Visitor name must be between 2 and 200 characters.");

        RuleFor(x => x.VisitorLicensePlate)
            .NotEmpty().WithMessage("Visitor license plate is required.")
            .Length(2, 20).WithMessage("Visitor license plate must be between 2 and 20 characters.");

        RuleFor(x => x.AccessExpiry)
            .NotEmpty().WithMessage("Access expiry time is required.")
            .GreaterThanOrEqualTo(x => x.EndDateTime).WithMessage("Access expiry must be greater than or equal to booking end time.");
    }
}
