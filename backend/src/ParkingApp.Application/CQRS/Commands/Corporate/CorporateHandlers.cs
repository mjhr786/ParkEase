using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Entities.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Events.Parking;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Application.CQRS.Commands.Corporate;

public class CreateCompanyHandler : ICommandHandler<CreateCompanyCommand, ApiResponse<CompanyDto>>
{
    private readonly IUnitOfWork _uow;

    public CreateCompanyHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<CompanyDto>> HandleAsync(CreateCompanyCommand command, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(command.UserId, ct);
        if (user == null)
        {
            return new ApiResponse<CompanyDto>(false, "User not found.", null);
        }

        if (await _uow.Companies.ExistsByRegistrationNumberAsync(command.Dto.RegistrationNumber, ct))
        {
            return new ApiResponse<CompanyDto>(false, "A company with this registration number already exists.", null);
        }

        var company = Company.Create(
            command.Dto.Name,
            command.Dto.RegistrationNumber,
            command.Dto.ContactEmail,
            command.Dto.ContactPhone,
            command.Dto.BillingAddress,
            command.Dto.BillingType,
            command.UserId);

        await _uow.Companies.AddAsync(company, ct);
        await _uow.SaveChangesAsync(ct);

        return new ApiResponse<CompanyDto>(true, "Company created successfully.", CorporateMapping.ToCompanyDto(company));
    }
}

public class AddMemberHandler : ICommandHandler<AddMemberCommand, ApiResponse<MembershipDto>>
{
    private readonly IUnitOfWork _uow;

    public AddMemberHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<MembershipDto>> HandleAsync(AddMemberCommand command, CancellationToken ct = default)
    {
        var targetUser = await _uow.Users.GetByEmailAsync(command.Dto.Email, ct);
        if (targetUser == null)
        {
            return new ApiResponse<MembershipDto>(false, "No ParkEase user found with this email. Please invite them instead.", null);
        }

        var company = await _uow.Companies.GetWithMembershipsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<MembershipDto>(false, "Company not found.", null);
        }

        try
        {
            var membership = company.AddMember(
                command.AdminUserId,
                targetUser.Id,
                command.Dto.Role,
                command.Dto.EmployeeCode,
                command.Dto.Priority);

            await _uow.SaveChangesAsync(ct);

            var dto = new MembershipDto(
                membership.Id,
                targetUser.Id,
                targetUser.FullName,
                targetUser.Email,
                membership.Role,
                membership.EmployeeCode,
                membership.Priority,
                membership.IsActive,
                membership.CreatedAt);

            return new ApiResponse<MembershipDto>(true, "Member added successfully.", dto);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<MembershipDto>(false, ex.Message, null);
        }
    }
}

public class InviteMemberHandler : ICommandHandler<InviteMemberCommand, ApiResponse<InvitationDto>>
{
    private readonly IUnitOfWork _uow;

    public InviteMemberHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<InvitationDto>> HandleAsync(InviteMemberCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetByIdAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<InvitationDto>(false, "Company not found.", null);
        }

        try
        {
            if (!company.IsActive)
            {
                return new ApiResponse<InvitationDto>(false, "This company is inactive.", null);
            }

            var adminMembership = await _uow.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
            if (adminMembership == null || !adminMembership.IsActive)
            {
                return new ApiResponse<InvitationDto>(false, "You are not an active member of this company.", null);
            }

            if (!adminMembership.IsAdmin)
            {
                return new ApiResponse<InvitationDto>(false, "Only company admins can perform this action.", null);
            }

            var normalizedEmail = command.Dto.Email.Trim().ToLowerInvariant();
            var existingUser = await _uow.Users.GetByEmailAsync(normalizedEmail, ct);
            var emailAlreadyBelongsToMember = existingUser != null
                && await _uow.Companies.IsUserMemberAsync(command.CompanyId, existingUser.Id, ct);

            if (emailAlreadyBelongsToMember)
            {
                return new ApiResponse<InvitationDto>(false, "This user is already a member of the company.", null);
            }

            if (await _uow.EmployeeInvitations.HasPendingInvitationAsync(command.CompanyId, normalizedEmail, ct))
            {
                return new ApiResponse<InvitationDto>(false, "There is already a pending invitation for this email address.", null);
            }

            var invitation = EmployeeInvitation.Create(command.CompanyId, normalizedEmail, command.Dto.Role, command.AdminUserId);
            await _uow.EmployeeInvitations.AddAsync(invitation, ct);
            await _uow.SaveChangesAsync(ct);

            var dto = new InvitationDto(
                invitation.Id,
                invitation.Email,
                invitation.Role,
                invitation.Status,
                invitation.ExpiresAt,
                invitation.CreatedAt);

            return new ApiResponse<InvitationDto>(true, "Invitation created successfully.", dto);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<InvitationDto>(false, ex.Message, null);
        }
    }
}

public class AcceptInvitationHandler : ICommandHandler<AcceptInvitationCommand, ApiResponse<MembershipDto>>
{
    private readonly IUnitOfWork _uow;

    public AcceptInvitationHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<MembershipDto>> HandleAsync(AcceptInvitationCommand command, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(command.UserId, ct);
        if (user == null)
        {
            return new ApiResponse<MembershipDto>(false, "User not found.", null);
        }

        var company = await _uow.Companies.GetAggregateForInvitationAcceptanceAsync(command.InvitationToken, command.UserId, ct);
        if (company == null)
        {
            return new ApiResponse<MembershipDto>(false, "Invalid or expired invitation.", null);
        }

        try
        {
            var membership = company.AcceptInvitation(command.InvitationToken, command.UserId, user.Email);
            await _uow.SaveChangesAsync(ct);

            var dto = new MembershipDto(
                membership.Id,
                user.Id,
                user.FullName,
                user.Email,
                membership.Role,
                membership.EmployeeCode,
                membership.Priority,
                membership.IsActive,
                membership.CreatedAt);

            return new ApiResponse<MembershipDto>(true, "Invitation accepted. You are now a member.", dto);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<MembershipDto>(false, ex.Message, null);
        }
    }
}

public class RemoveMemberHandler : ICommandHandler<RemoveMemberCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _uow;

    public RemoveMemberHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<bool>> HandleAsync(RemoveMemberCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetWithMembershipsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<bool>(false, "Company not found.", false);
        }

        try
        {
            company.RemoveMember(command.AdminUserId, command.MembershipId);
            await _uow.SaveChangesAsync(ct);

            return new ApiResponse<bool>(true, "Member removed successfully.", true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<bool>(false, ex.Message, false);
        }
    }
}

public class CreateCorporateParkingSpaceHandler : ICommandHandler<CreateCorporateParkingSpaceCommand, ApiResponse<CorporateParkingSpaceDto>>
{
    private readonly IUnitOfWork _uow;

    public CreateCorporateParkingSpaceHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<CorporateParkingSpaceDto>> HandleAsync(CreateCorporateParkingSpaceCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetWithMembershipsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Company not found.", null);
        }

        var adminMembership = company.Memberships.FirstOrDefault(m => m.UserId == command.AdminUserId && !m.IsDeleted);
        if (adminMembership == null || !adminMembership.IsActive || !adminMembership.IsAdmin)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Only company admins can create company-owned parking.", null);
        }

        var adminUser = await _uow.Users.GetByIdAsync(command.AdminUserId, ct);
        if (adminUser == null)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Admin user not found.", null);
        }

        var parking = command.Dto.ToEntity(command.AdminUserId);
        parking.CompanyOwnerId = command.CompanyId;
        parking.OwnershipType = ParkingSpaceOwnershipType.CompanyOwned;
        parking.IsCorporateOnly = true;
        parking.IsVerified = true;

        await _uow.ParkingSpaces.AddAsync(parking, ct);
        await _uow.SaveChangesAsync(ct);

        return new ApiResponse<CorporateParkingSpaceDto>(
            true,
            "Company-owned parking space created.",
            CorporateMapping.ToCorporateParkingSpaceDto(parking, command.CompanyId));
    }
}

public class ToggleCorporateParkingSpaceHandler : ICommandHandler<ToggleCorporateParkingSpaceCommand, ApiResponse<CorporateParkingSpaceDto>>
{
    private readonly IUnitOfWork _uow;

    public ToggleCorporateParkingSpaceHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<CorporateParkingSpaceDto>> HandleAsync(ToggleCorporateParkingSpaceCommand command, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership == null || !membership.IsActive || !membership.IsAdmin)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Only company admins can update company-owned parking.", null);
        }

        var parking = await _uow.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, ct);
        if (parking == null || parking.CompanyOwnerId != command.CompanyId || parking.OwnershipType != ParkingSpaceOwnershipType.CompanyOwned)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Company-owned parking space not found.", null);
        }

        parking.IsActive = !parking.IsActive;
        _uow.ParkingSpaces.Update(parking);
        await _uow.SaveChangesAsync(ct);

        return new ApiResponse<CorporateParkingSpaceDto>(
            true,
            parking.IsActive ? "Parking space activated." : "Parking space deactivated.",
            CorporateMapping.ToCorporateParkingSpaceDto(parking, command.CompanyId));
    }
}

public class UpdateCorporateParkingSpaceHandler : ICommandHandler<UpdateCorporateParkingSpaceCommand, ApiResponse<CorporateParkingSpaceDto>>
{
    private readonly IUnitOfWork _uow;

    public UpdateCorporateParkingSpaceHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<CorporateParkingSpaceDto>> HandleAsync(UpdateCorporateParkingSpaceCommand command, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership == null || !membership.IsActive || !membership.IsAdmin)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Only company admins can edit company-owned parking.", null);
        }

        var parking = await _uow.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, ct);
        if (parking == null || parking.CompanyOwnerId != command.CompanyId || parking.OwnershipType != ParkingSpaceOwnershipType.CompanyOwned)
        {
            return new ApiResponse<CorporateParkingSpaceDto>(false, "Company-owned parking space not found.", null);
        }

        var dto = command.Dto;
        if (!string.IsNullOrWhiteSpace(dto.Title)) parking.Title = dto.Title.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Description)) parking.Description = dto.Description.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Address)) parking.Address = dto.Address.Trim();
        if (!string.IsNullOrWhiteSpace(dto.City)) parking.City = dto.City.Trim();
        if (!string.IsNullOrWhiteSpace(dto.State)) parking.State = dto.State.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Country)) parking.Country = dto.Country.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PostalCode)) parking.PostalCode = dto.PostalCode.Trim();
        if (dto.ZoneCode != null) parking.ZoneCode = string.IsNullOrWhiteSpace(dto.ZoneCode) ? null : dto.ZoneCode.Trim().ToUpperInvariant();
        if (dto.Latitude.HasValue) parking.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) parking.Longitude = dto.Longitude.Value;
        if (dto.Latitude.HasValue || dto.Longitude.HasValue)
        {
            parking.Location = new NetTopologySuite.Geometries.Point(parking.Longitude, parking.Latitude) { SRID = 4326 };
        }

        if (dto.ParkingType.HasValue) parking.ParkingType = dto.ParkingType.Value;
        if (dto.TotalSpots.HasValue)
        {
            if (dto.TotalSpots.Value < 1)
            {
                return new ApiResponse<CorporateParkingSpaceDto>(false, "Total spots must be at least 1.", null);
            }

            parking.TotalSpots = dto.TotalSpots.Value;
            parking.AvailableSpots = Math.Min(parking.AvailableSpots, parking.TotalSpots);
        }

        if (dto.HourlyRate.HasValue) parking.HourlyRate = dto.HourlyRate.Value;
        if (dto.DailyRate.HasValue) parking.DailyRate = dto.DailyRate.Value;
        if (dto.WeeklyRate.HasValue) parking.WeeklyRate = dto.WeeklyRate.Value;
        if (dto.MonthlyRate.HasValue) parking.MonthlyRate = dto.MonthlyRate.Value;
        if (dto.OpenTime.HasValue) parking.OpenTime = dto.OpenTime.Value;
        if (dto.CloseTime.HasValue) parking.CloseTime = dto.CloseTime.Value;
        if (dto.Is24Hours.HasValue) parking.Is24Hours = dto.Is24Hours.Value;
        if (dto.Amenities != null) parking.Amenities = string.Join(",", dto.Amenities);
        if (dto.AllowedVehicleTypes != null) parking.AllowedVehicleTypes = string.Join(",", dto.AllowedVehicleTypes.Select(v => v.ToString()));
        if (dto.ImageUrls != null) parking.ImageUrls = string.Join(",", dto.ImageUrls);
        if (dto.SpecialInstructions != null) parking.SpecialInstructions = dto.SpecialInstructions.Trim();
        parking.UpdatedAt = DateTime.UtcNow;
        parking.AddDomainEvent(new ParkingSpaceUpdatedEvent(parking.Id, parking.Title));

        _uow.ParkingSpaces.Update(parking);
        await _uow.SaveChangesAsync(ct);

        return new ApiResponse<CorporateParkingSpaceDto>(
            true,
            "Company-owned parking space updated.",
            CorporateMapping.ToCorporateParkingSpaceDto(parking, command.CompanyId));
    }
}

public class RetireCorporateParkingSpaceHandler : ICommandHandler<RetireCorporateParkingSpaceCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _uow;

    public RetireCorporateParkingSpaceHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<bool>> HandleAsync(RetireCorporateParkingSpaceCommand command, CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership == null || !membership.IsActive || !membership.IsAdmin)
        {
            return new ApiResponse<bool>(false, "Only company admins can retire company-owned parking.", false);
        }

        var parking = await _uow.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, ct);
        if (parking == null || parking.CompanyOwnerId != command.CompanyId || parking.OwnershipType != ParkingSpaceOwnershipType.CompanyOwned)
        {
            return new ApiResponse<bool>(false, "Company-owned parking space not found.", false);
        }

        var company = await _uow.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        var hasActiveAllocation = company?.Allocations.Any(a =>
            a.ParkingSpaceId == command.ParkingSpaceId &&
            !a.IsDeleted &&
            a.Status is AllocationStatus.Active or AllocationStatus.PendingApproval) == true;

        if (hasActiveAllocation)
        {
            return new ApiResponse<bool>(false, "Deactivate or let active allocations expire before retiring this parking space.", false);
        }

        var now = DateTime.UtcNow;
        var hasActiveBookings = await _uow.Bookings.AnyAsync(b =>
            b.ParkingSpaceId == command.ParkingSpaceId &&
            (b.Status == BookingStatus.Confirmed ||
             b.Status == BookingStatus.InProgress ||
             b.Status == BookingStatus.Pending ||
             b.Status == BookingStatus.AwaitingPayment) &&
            b.EndDateTime > now,
            ct);

        if (hasActiveBookings)
        {
            return new ApiResponse<bool>(false, "Cannot retire parking space with active bookings.", false);
        }

        parking.IsActive = false;
        parking.IsDeleted = true;
        parking.UpdatedAt = DateTime.UtcNow;
        parking.AddDomainEvent(new ParkingSpaceDeletedEvent(parking.Id, command.AdminUserId));

        _uow.ParkingSpaces.Update(parking);
        await _uow.SaveChangesAsync(ct);

        return new ApiResponse<bool>(true, "Company-owned parking space retired.", true);
    }
}

public class AllocateParkingSlotsHandler : ICommandHandler<AllocateParkingSlotsCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICompanyQuotaCache _quotaCache;

    public AllocateParkingSlotsHandler(IUnitOfWork uow, ICompanyQuotaCache quotaCache)
    {
        _uow = uow;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(AllocateParkingSlotsCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Company not found.", null);
        }

        var parkingSpace = await _uow.ParkingSpaces.GetByIdAsync(command.Dto.ParkingSpaceId, ct);
        if (parkingSpace == null || !parkingSpace.IsActive)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Parking space not found or inactive.", null);
        }

        if (parkingSpace.OwnershipType == ParkingSpaceOwnershipType.CompanyOwned)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Company-owned parking must use the internal corporate allocation flow.", null);
        }

        try
        {
            var quota = Quota.Create(command.Dto.TotalSlots, command.Dto.FixedSlots, command.Dto.SharedSlots);
            var policy = CorporateCommandHelpers.CreateBookingPolicy(command.Dto.Policy);

            var allocation = company.RequestAllocation(
                command.AdminUserId,
                command.Dto.ParkingSpaceId,
                quota,
                command.Dto.MonthlyRate,
                command.Dto.StartDate,
                command.Dto.EndDate,
                parkingSpace.TotalSpots,
                policy);

            allocation.SetVendorLeaseMetadata(parkingSpace.OwnerId, command.Dto.LeaseReference);

            await _uow.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Parking allocation created. Awaiting parking space owner approval.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace.Title));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}

public class CreateOwnedParkingAllocationHandler : ICommandHandler<CreateOwnedParkingAllocationCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICompanyQuotaCache _quotaCache;

    public CreateOwnedParkingAllocationHandler(IUnitOfWork uow, ICompanyQuotaCache quotaCache)
    {
        _uow = uow;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(CreateOwnedParkingAllocationCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Company not found.", null);
        }

        var parkingSpace = await _uow.ParkingSpaces.GetByIdAsync(command.Dto.ParkingSpaceId, ct);
        if (parkingSpace == null || !parkingSpace.IsActive)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Company-owned parking space not found or inactive.", null);
        }

        if (parkingSpace.CompanyOwnerId != command.CompanyId || parkingSpace.OwnershipType != ParkingSpaceOwnershipType.CompanyOwned)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "This parking space is not owned by the selected company.", null);
        }

        try
        {
            var quota = Quota.Create(command.Dto.TotalSlots, command.Dto.FixedSlots, command.Dto.SharedSlots);
            var policy = CorporateCommandHelpers.CreateBookingPolicy(command.Dto.Policy);

            var allocation = company.CreateOwnedParkingAllocation(
                command.AdminUserId,
                command.Dto.ParkingSpaceId,
                quota,
                command.Dto.MonthlyRate,
                command.Dto.StartDate,
                command.Dto.EndDate,
                parkingSpace.TotalSpots,
                policy);

            await _uow.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Company-owned parking allocation activated.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace.Title));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}

public class ApproveAllocationHandler : ICommandHandler<ApproveAllocationCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICompanyQuotaCache _quotaCache;

    public ApproveAllocationHandler(IUnitOfWork uow, ICompanyQuotaCache quotaCache)
    {
        _uow = uow;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(ApproveAllocationCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetAggregateByAllocationAsync(command.AllocationId, ct);
        if (company == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);
        }

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.AllocationId && !a.IsDeleted);
        if (allocation == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);
        }

        var parkingSpace = await _uow.ParkingSpaces.GetByIdAsync(allocation.ParkingSpaceId, ct);
        if (parkingSpace == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Parking space not found.", null);
        }

        if (allocation.SourceType != ParkingAllocationSource.VendorLease)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Only vendor-leased allocations require vendor approval.", null);
        }

        if (parkingSpace.OwnerId != command.ParkingOwnerUserId)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Only the parking space owner can approve allocations.", null);
        }

        try
        {
            company.ApproveAllocation(command.AllocationId, command.ParkingOwnerUserId);
            await _uow.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Allocation approved.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace.Title));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}

public class RejectAllocationHandler : ICommandHandler<RejectAllocationCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICompanyQuotaCache _quotaCache;

    public RejectAllocationHandler(IUnitOfWork uow, ICompanyQuotaCache quotaCache)
    {
        _uow = uow;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(RejectAllocationCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetAggregateByAllocationAsync(command.AllocationId, ct);
        if (company == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);
        }

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.AllocationId && !a.IsDeleted);
        if (allocation == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);
        }

        var parkingSpace = await _uow.ParkingSpaces.GetByIdAsync(allocation.ParkingSpaceId, ct);
        if (parkingSpace == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Parking space not found.", null);
        }

        if (allocation.SourceType != ParkingAllocationSource.VendorLease)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Only vendor-leased allocations can be rejected by a parking owner.", null);
        }

        if (parkingSpace.OwnerId != command.ParkingOwnerUserId)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Only the parking space owner can reject allocations.", null);
        }

        try
        {
            company.RejectAllocation(command.AllocationId, command.Reason);
            await _uow.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Allocation rejected.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace.Title));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}

public class BookCorporateParkingHandler : ICommandHandler<BookCorporateParkingCommand, ApiResponse<CorporateReservationResultDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public BookCorporateParkingHandler(IUnitOfWork uow, ICacheService cache, ICompanyQuotaCache quotaCache)
    {
        _uow = uow;
        _cache = cache;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<CorporateReservationResultDto>> HandleAsync(BookCorporateParkingCommand command, CancellationToken ct = default)
    {
        var quota = await _quotaCache.GetAllocationAsync(command.CompanyId, command.Dto.AllocationId, ct);
        if (quota == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Allocation not found.", null);
        }

        if (!quota.IsBookable)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Active allocation not found.", null);
        }

        var usageDate = DateOnly.FromDateTime(command.Dto.StartDateTime);
        var company = await _uow.Companies.GetAggregateForBookingAsync(
            command.CompanyId,
            command.UserId,
            command.Dto.AllocationId,
            command.Dto.StartDateTime,
            command.Dto.EndDateTime,
            ct);
        if (company == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Company not found.", null);
        }

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.Dto.AllocationId && !a.IsDeleted);
        if (allocation == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Allocation not found.", null);
        }

        var lockKey = CorporateCommandHelpers.BuildLockKey(command.CompanyId, allocation.Id, command.Dto.StartDateTime);
        if (!await _cache.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10), ct))
        {
            return new ApiResponse<CorporateReservationResultDto>(
                false,
                "System is processing other bookings for this allocation. Please try again in a few seconds.",
                null);
        }

        Booking? booking = null;
        CorporateReservationOutcome? reservation = null;

        try
        {
            var membership = company.Memberships.FirstOrDefault(m => m.UserId == command.UserId && !m.IsDeleted);
            if (membership == null)
            {
                return new ApiResponse<CorporateReservationResultDto>(false, "You are not an active member of this company.", null);
            }

            var weekStart = CorporateCommandHelpers.GetWeekStart(usageDate);
            var dayCount = await _uow.CorporateBookings.GetMembershipBookingCountForDateAsync(command.CompanyId, membership.Id, usageDate, ct);
            var weekCount = await _uow.CorporateBookings.GetMembershipBookingCountForWeekAsync(command.CompanyId, membership.Id, weekStart, ct);
            var activeSharedCount = await _uow.CorporateBookings.GetActiveSharedBookingsCountAsync(
                command.CompanyId,
                allocation.Id,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                ct);
            var occupiedSharedSlotNumbers = await _uow.CorporateBookings.GetOccupiedSharedSlotNumbersAsync(
                command.CompanyId,
                allocation.Id,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                ct);
            var sharedSlotUsageBySlot = await _uow.CorporateBookings.GetSharedSlotUsageCountsAsync(
                command.CompanyId,
                allocation.Id,
                DateTime.UtcNow.AddDays(-30),
                ct);
            var anonymousOccupiedSharedBookings = Math.Max(0, activeSharedCount - occupiedSharedSlotNumbers.Count);
            var hasOverlappingBooking = await _uow.CorporateBookings.HasOverlappingBookingAsync(
                command.CompanyId,
                membership.Id,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                ct);
            var hasOverlappingVehicleBooking = !string.IsNullOrWhiteSpace(command.Dto.VehicleNumber)
                && await _uow.CorporateBookings.HasOverlappingVehicleBookingAsync(
                    command.CompanyId,
                    allocation.Id,
                    command.Dto.VehicleNumber!,
                    command.Dto.StartDateTime,
                    command.Dto.EndDateTime,
                    ct);
            var recentBookingCreations = await _uow.CorporateBookings.GetRecentBookingCreateCountAsync(
                command.CompanyId,
                membership.Id,
                DateTime.UtcNow.AddHours(-24),
                ct);

            var duration = command.Dto.EndDateTime - command.Dto.StartDateTime;
            var amount = company.CalculateBookingAmount(quota.HourlyRate, duration);
            booking = CorporateCommandHelpers.CreateEmployeeBooking(command, quota.ParkingSpaceId, amount);
            var fraudAssessment = company.AssessFraudRisk(
                command.UserId,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                hasOverlappingBooking,
                hasOverlappingVehicleBooking,
                recentBookingCreations);

            reservation = company.ReserveEmployeeParking(
                command.UserId,
                allocation.Id,
                booking,
                dayCount,
                weekCount,
                occupiedSharedSlotNumbers,
                sharedSlotUsageBySlot,
                anonymousOccupiedSharedBookings,
                fraudAssessment);

            if (!reservation.IsWaitlisted)
            {
                await _uow.Bookings.AddAsync(booking, ct);
            }

            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, ex.Message, null);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, ct);
        }

        var message = reservation!.IsWaitlisted
            ? "No shared slot is available right now. Added to waitlist."
            : "Corporate parking booked successfully.";

        return new ApiResponse<CorporateReservationResultDto>(
            true,
            message,
            CorporateMapping.ToReservationResultDto(reservation, booking, company));
    }
}

public class BookVisitorParkingHandler : ICommandHandler<BookVisitorParkingCommand, ApiResponse<CorporateReservationResultDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public BookVisitorParkingHandler(IUnitOfWork uow, ICacheService cache, ICompanyQuotaCache quotaCache)
    {
        _uow = uow;
        _cache = cache;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<CorporateReservationResultDto>> HandleAsync(BookVisitorParkingCommand command, CancellationToken ct = default)
    {
        var quota = await _quotaCache.GetAllocationAsync(command.CompanyId, command.Dto.AllocationId, ct);
        if (quota == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Allocation not found.", null);
        }

        if (!quota.IsBookable)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Active allocation not found.", null);
        }

        var company = await _uow.Companies.GetAggregateForBookingAsync(
            command.CompanyId,
            command.UserId,
            command.Dto.AllocationId,
            command.Dto.StartDateTime,
            command.Dto.EndDateTime,
            ct);
        if (company == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Company not found.", null);
        }

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.Dto.AllocationId && !a.IsDeleted);
        if (allocation == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Allocation not found.", null);
        }

        var lockKey = CorporateCommandHelpers.BuildLockKey(command.CompanyId, allocation.Id, command.Dto.StartDateTime);
        if (!await _cache.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10), ct))
        {
            return new ApiResponse<CorporateReservationResultDto>(
                false,
                "System is processing other bookings for this allocation. Please try again in a few seconds.",
                null);
        }

        Booking? booking = null;
        CorporateReservationOutcome? reservation = null;

        try
        {
            var membership = company.Memberships.FirstOrDefault(m => m.UserId == command.UserId && !m.IsDeleted);
            if (membership == null)
            {
                return new ApiResponse<CorporateReservationResultDto>(false, "You are not an active member of this company.", null);
            }

            var activeSharedCount = await _uow.CorporateBookings.GetActiveSharedBookingsCountAsync(
                command.CompanyId,
                allocation.Id,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                ct);
            var occupiedSharedSlotNumbers = await _uow.CorporateBookings.GetOccupiedSharedSlotNumbersAsync(
                command.CompanyId,
                allocation.Id,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                ct);
            var sharedSlotUsageBySlot = await _uow.CorporateBookings.GetSharedSlotUsageCountsAsync(
                command.CompanyId,
                allocation.Id,
                DateTime.UtcNow.AddDays(-30),
                ct);
            var anonymousOccupiedSharedBookings = Math.Max(0, activeSharedCount - occupiedSharedSlotNumbers.Count);
            var hasOverlappingVehicleBooking = await _uow.CorporateBookings.HasOverlappingVehicleBookingAsync(
                command.CompanyId,
                allocation.Id,
                command.Dto.VisitorLicensePlate,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                ct);
            var recentBookingCreations = await _uow.CorporateBookings.GetRecentBookingCreateCountAsync(
                command.CompanyId,
                membership.Id,
                DateTime.UtcNow.AddHours(-24),
                ct);

            var duration = command.Dto.EndDateTime - command.Dto.StartDateTime;
            var amount = company.CalculateBookingAmount(quota.HourlyRate, duration);
            booking = CorporateCommandHelpers.CreateVisitorBooking(command, quota.ParkingSpaceId, amount);
            var fraudAssessment = company.AssessFraudRisk(
                command.UserId,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                hasOverlappingMemberBooking: false,
                hasOverlappingVehicleBooking,
                recentBookingCreations);

            reservation = company.ReserveVisitorParking(
                command.UserId,
                allocation.Id,
                booking,
                command.Dto.VisitorName,
                command.Dto.VisitorLicensePlate,
                command.Dto.AccessExpiry,
                occupiedSharedSlotNumbers,
                sharedSlotUsageBySlot,
                anonymousOccupiedSharedBookings,
                fraudAssessment);

            if (!reservation.IsWaitlisted)
            {
                await _uow.Bookings.AddAsync(booking, ct);
                booking.QRCode = reservation.Booking!.AccessPolicy?.QrCodeToken;
            }

            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, ex.Message, null);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, ct);
        }

        var message = reservation!.IsWaitlisted
            ? "No shared slot is available right now. Added visitor request to waitlist."
            : "Visitor parking booked successfully.";

        return new ApiResponse<CorporateReservationResultDto>(
            true,
            message,
            CorporateMapping.ToReservationResultDto(reservation, booking, company));
    }
}

public class UpdateBookingPolicyHandler : ICommandHandler<UpdateBookingPolicyCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICompanyQuotaCache _quotaCache;

    public UpdateBookingPolicyHandler(IUnitOfWork uow, ICompanyQuotaCache quotaCache)
    {
        _uow = uow;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(UpdateBookingPolicyCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Company not found.", null);
        }

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.AllocationId && !a.IsDeleted);
        if (allocation == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);
        }

        try
        {
            var policy = CorporateCommandHelpers.CreateBookingPolicy(command.Policy) ?? BookingPolicy.Default();
            company.UpdateAllocationPolicy(command.AdminUserId, command.AllocationId, policy);

            await _uow.SaveChangesAsync(ct);
            await _quotaCache.InvalidateCompanyAsync(company.Id, ct);

            var parkingSpace = await _uow.ParkingSpaces.GetByIdAsync(allocation.ParkingSpaceId, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Booking policy updated.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace?.Title ?? string.Empty));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}

public class AssignFixedSlotHandler : ICommandHandler<AssignFixedSlotCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly IUnitOfWork _uow;

    public AssignFixedSlotHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(AssignFixedSlotCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Company not found.", null);
        }

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.AllocationId && !a.IsDeleted);
        if (allocation == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);
        }

        try
        {
            company.AssignFixedSlot(command.AdminUserId, command.AllocationId, command.Dto.MembershipId, command.Dto.SlotNumber);
            await _uow.SaveChangesAsync(ct);

            var parkingSpace = await _uow.ParkingSpaces.GetByIdAsync(allocation.ParkingSpaceId, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Fixed slot assigned.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace?.Title ?? string.Empty));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}

public class RemoveFixedSlotHandler : ICommandHandler<RemoveFixedSlotCommand, ApiResponse<ParkingAllocationDto>>
{
    private readonly IUnitOfWork _uow;

    public RemoveFixedSlotHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<ParkingAllocationDto>> HandleAsync(RemoveFixedSlotCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Company not found.", null);
        }

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.AllocationId && !a.IsDeleted);
        if (allocation == null)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Allocation not found.", null);
        }

        var adminMembership = company.Memberships.FirstOrDefault(m => m.UserId == command.AdminUserId && !m.IsDeleted);
        if (adminMembership == null || !adminMembership.IsActive || !adminMembership.IsAdmin)
        {
            return new ApiResponse<ParkingAllocationDto>(false, "Only company admins can perform this action.", null);
        }

        try
        {
            allocation.RemoveFixedAssignment(command.MembershipId);
            await _uow.SaveChangesAsync(ct);

            var parkingSpace = await _uow.ParkingSpaces.GetByIdAsync(allocation.ParkingSpaceId, ct);

            return new ApiResponse<ParkingAllocationDto>(
                true,
                "Fixed slot assignment removed.",
                CorporateMapping.ToAllocationDto(allocation, parkingSpace?.Title ?? string.Empty));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<ParkingAllocationDto>(false, ex.Message, null);
        }
    }
}

public class CancelWaitlistEntryHandler : ICommandHandler<CancelWaitlistEntryCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _uow;

    public CancelWaitlistEntryHandler(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<bool>> HandleAsync(CancelWaitlistEntryCommand command, CancellationToken ct = default)
    {
        var company = await _uow.Companies.GetFullAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<bool>(false, "Company not found.", false);
        }

        try
        {
            company.CancelWaitlistEntry(command.UserId, command.WaitlistEntryId);
            await _uow.SaveChangesAsync(ct);

            return new ApiResponse<bool>(true, "Waitlist entry cancelled.", true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<bool>(false, ex.Message, false);
        }
    }
}

public class PromoteWaitlistEntryHandler : ICommandHandler<PromoteWaitlistEntryCommand, ApiResponse<CorporateReservationResultDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public PromoteWaitlistEntryHandler(IUnitOfWork uow, ICacheService cache, ICompanyQuotaCache quotaCache)
    {
        _uow = uow;
        _cache = cache;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<CorporateReservationResultDto>> HandleAsync(PromoteWaitlistEntryCommand command, CancellationToken ct = default)
    {
        var snapshot = await _uow.Companies.GetFullAsync(command.CompanyId, ct);
        if (snapshot == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Company not found.", null);
        }

        var adminMembership = snapshot.Memberships.FirstOrDefault(m => m.UserId == command.AdminUserId && !m.IsDeleted);
        if (adminMembership == null || !adminMembership.IsActive || !adminMembership.IsAdmin)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Only company admins can promote waitlist entries.", null);
        }

        var waitlistEntry = snapshot.WaitlistEntries.FirstOrDefault(w => w.Id == command.WaitlistEntryId && !w.IsDeleted);
        if (waitlistEntry == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Waitlist entry not found.", null);
        }

        if (waitlistEntry.Status != WaitlistStatus.Pending)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Only pending waitlist entries can be promoted.", null);
        }

        var targetMembership = snapshot.Memberships.FirstOrDefault(m => m.Id == waitlistEntry.MembershipId && !m.IsDeleted);
        if (targetMembership == null || !targetMembership.IsActive)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Waitlist member is no longer active.", null);
        }

        var quota = await _quotaCache.GetAllocationAsync(command.CompanyId, waitlistEntry.AllocationId, ct);
        if (quota == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Allocation not found.", null);
        }

        if (!quota.IsBookable)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Active allocation not found.", null);
        }

        var lockKey = CorporateCommandHelpers.BuildLockKey(command.CompanyId, waitlistEntry.AllocationId, waitlistEntry.RequestedStartDateTime);
        if (!await _cache.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10), ct))
        {
            return new ApiResponse<CorporateReservationResultDto>(
                false,
                "System is processing other bookings for this allocation. Please try again in a few seconds.",
                null);
        }

        Booking? booking = null;
        CorporateReservationOutcome? reservation = null;
        Company? company = null;

        try
        {
            company = await _uow.Companies.GetAggregateForBookingAsync(
                command.CompanyId,
                targetMembership.UserId,
                waitlistEntry.AllocationId,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                ct);
            if (company == null)
            {
                return new ApiResponse<CorporateReservationResultDto>(false, "Company not found.", null);
            }

            var allocation = company.Allocations.FirstOrDefault(a => a.Id == waitlistEntry.AllocationId && !a.IsDeleted);
            if (allocation == null)
            {
                return new ApiResponse<CorporateReservationResultDto>(false, "Allocation not found.", null);
            }

            var activeSharedCount = await _uow.CorporateBookings.GetActiveSharedBookingsCountAsync(
                command.CompanyId,
                allocation.Id,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                ct);
            var occupiedSharedSlotNumbers = await _uow.CorporateBookings.GetOccupiedSharedSlotNumbersAsync(
                command.CompanyId,
                allocation.Id,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                ct);
            var sharedSlotUsageBySlot = await _uow.CorporateBookings.GetSharedSlotUsageCountsAsync(
                command.CompanyId,
                allocation.Id,
                DateTime.UtcNow.AddDays(-30),
                ct);
            var anonymousOccupiedSharedBookings = Math.Max(0, activeSharedCount - occupiedSharedSlotNumbers.Count);
            var recentBookingCreations = await _uow.CorporateBookings.GetRecentBookingCreateCountAsync(
                command.CompanyId,
                targetMembership.Id,
                DateTime.UtcNow.AddHours(-24),
                ct);

            var duration = waitlistEntry.RequestedEndDateTime - waitlistEntry.RequestedStartDateTime;
            var amount = company.CalculateBookingAmount(quota.HourlyRate, duration);
            booking = CorporateCommandHelpers.CreateBookingFromWaitlist(waitlistEntry, targetMembership.UserId, quota.ParkingSpaceId, amount);

            var hasOverlappingBooking = await _uow.CorporateBookings.HasOverlappingBookingAsync(
                command.CompanyId,
                targetMembership.Id,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                ct);
            var vehicleNumber = waitlistEntry.IsVisitorBooking ? waitlistEntry.VisitorLicensePlate : waitlistEntry.VehicleNumber;
            var hasOverlappingVehicleBooking = !string.IsNullOrWhiteSpace(vehicleNumber)
                && await _uow.CorporateBookings.HasOverlappingVehicleBookingAsync(
                    command.CompanyId,
                    allocation.Id,
                    vehicleNumber!,
                    waitlistEntry.RequestedStartDateTime,
                    waitlistEntry.RequestedEndDateTime,
                    ct);

            var fraudAssessment = company.AssessFraudRisk(
                targetMembership.UserId,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                hasOverlappingBooking,
                hasOverlappingVehicleBooking,
                recentBookingCreations);

            if (waitlistEntry.IsVisitorBooking)
            {
                reservation = company.ReserveVisitorParking(
                    targetMembership.UserId,
                    allocation.Id,
                    booking,
                    waitlistEntry.VisitorName ?? string.Empty,
                    waitlistEntry.VisitorLicensePlate ?? string.Empty,
                    waitlistEntry.AccessExpiryUtc ?? waitlistEntry.RequestedEndDateTime,
                    occupiedSharedSlotNumbers,
                    sharedSlotUsageBySlot,
                    anonymousOccupiedSharedBookings,
                    fraudAssessment);
            }
            else
            {
                var usageDate = DateOnly.FromDateTime(waitlistEntry.RequestedStartDateTime);
                var weekStart = CorporateCommandHelpers.GetWeekStart(usageDate);
                var dayCount = await _uow.CorporateBookings.GetMembershipBookingCountForDateAsync(command.CompanyId, targetMembership.Id, usageDate, ct);
                var weekCount = await _uow.CorporateBookings.GetMembershipBookingCountForWeekAsync(command.CompanyId, targetMembership.Id, weekStart, ct);

                reservation = company.ReserveEmployeeParking(
                    targetMembership.UserId,
                    allocation.Id,
                    booking,
                    dayCount,
                    weekCount,
                    occupiedSharedSlotNumbers,
                    sharedSlotUsageBySlot,
                    anonymousOccupiedSharedBookings,
                    fraudAssessment);
            }

            if (reservation.IsWaitlisted)
            {
                return new ApiResponse<CorporateReservationResultDto>(
                    false,
                    "This waitlist entry cannot be promoted yet. It may not be first in line or no shared slot is available.",
                    CorporateMapping.ToReservationResultDto(reservation, booking, company));
            }

            await _uow.Bookings.AddAsync(booking, ct);
            if (waitlistEntry.IsVisitorBooking)
            {
                booking.QRCode = reservation.Booking!.AccessPolicy?.QrCodeToken;
            }

            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, ex.Message, null);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, ct);
        }

        return new ApiResponse<CorporateReservationResultDto>(
            true,
            "Waitlist entry promoted to a confirmed corporate booking.",
            CorporateMapping.ToReservationResultDto(reservation!, booking, company!));
    }
}

internal static class CorporateMapping
{
    public static CompanyDto ToCompanyDto(Company company) => new(
        company.Id,
        company.Name,
        company.RegistrationNumber,
        company.ContactEmail,
        company.ContactPhone,
        company.BillingAddress,
        company.BillingType,
        company.IsActive,
        company.Memberships.Count(m => !m.IsDeleted),
        company.Allocations.Count(a => a.Status == AllocationStatus.Active && !a.IsDeleted),
        company.CreatedAt);

    public static ParkingAllocationDto ToAllocationDto(ParkingAllocation allocation, string parkingSpaceTitle) => new(
        allocation.Id,
        allocation.CompanyId,
        allocation.ParkingSpaceId,
        parkingSpaceTitle,
        allocation.Quota.TotalSlots,
        allocation.Quota.FixedSlots,
        allocation.Quota.SharedSlots,
        allocation.MonthlyRate,
        allocation.StartDate,
        allocation.EndDate,
        allocation.Status,
        allocation.SourceType,
        allocation.VendorId,
        allocation.LeaseReference,
        allocation.ApprovedByUserId,
        allocation.ApprovedAt,
        new BookingPolicyDto(
            allocation.BookingPolicy.MaxBookingsPerEmployeePerDay,
            allocation.BookingPolicy.MaxBookingsPerEmployeePerWeek,
            allocation.BookingPolicy.PriorityThreshold,
            allocation.BookingPolicy.AllowedStartTime,
            allocation.BookingPolicy.AllowedEndTime,
            allocation.BookingPolicy.AllowWeekends),
        allocation.FixedAssignments
            .Where(f => !f.IsDeleted)
            .Select(f => new FixedSlotAssignmentDto(f.MembershipId, string.Empty, f.SlotNumber, f.AssignedAt))
            .ToList(),
        allocation.CreatedAt);

    public static CorporateParkingSpaceDto ToCorporateParkingSpaceDto(ParkingSpace parking, Guid companyId) => new(
        parking.Id,
        companyId,
        parking.Title,
        parking.Description,
        parking.Address,
        parking.City,
        parking.State,
        parking.Country,
        parking.PostalCode,
        parking.Latitude,
        parking.Longitude,
        parking.ParkingType,
        parking.TotalSpots,
        parking.AvailableSpots,
        parking.HourlyRate,
        parking.DailyRate,
        parking.WeeklyRate,
        parking.MonthlyRate,
        parking.OpenTime,
        parking.CloseTime,
        parking.Is24Hours,
        ParseCommaSeparated(parking.Amenities),
        ParseVehicleTypes(parking.AllowedVehicleTypes),
        ParseCommaSeparated(parking.ImageUrls),
        parking.IsActive,
        parking.IsVerified,
        parking.SpecialInstructions,
        parking.ZoneCode,
        parking.CreatedAt);

    public static CorporateBookingDto ToCorporateBookingDto(CorporateBooking corporateBooking, Booking booking) => new(
        corporateBooking.Id,
        booking.Id,
        booking.BookingReference,
        corporateBooking.SlotType,
        booking.SlotNumber,
        corporateBooking.IsVisitorBooking,
        corporateBooking.VisitorName,
        corporateBooking.VisitorLicensePlate,
        booking.StartDateTime,
        booking.EndDateTime,
        booking.Status,
        corporateBooking.AccessPolicy?.QrCodeToken ?? booking.QRCode,
        corporateBooking.CreatedAt);

    public static CorporateWaitlistDto ToWaitlistDto(CorporateWaitlistEntry waitlistEntry, int position) => new(
        waitlistEntry.Id,
        waitlistEntry.AllocationId,
        waitlistEntry.IsVisitorBooking,
        waitlistEntry.RequestedStartDateTime,
        waitlistEntry.RequestedEndDateTime,
        waitlistEntry.VehicleNumber,
        waitlistEntry.VisitorName,
        waitlistEntry.VisitorLicensePlate,
        waitlistEntry.Status,
        waitlistEntry.PriorityAtRequest,
        position,
        waitlistEntry.CreatedAt);

    public static FraudAssessmentDto ToFraudAssessmentDto(CorporateFraudAssessment fraudAssessment) => new(
        fraudAssessment.RiskLevel,
        fraudAssessment.IsBlocked,
        fraudAssessment.Reason);

    public static CorporateReservationResultDto ToReservationResultDto(CorporateReservationOutcome reservation, Booking? booking, Company company)
    {
        var bookingDto = reservation.Booking != null && booking != null
            ? ToCorporateBookingDto(reservation.Booking, booking)
            : null;

        var waitlistDto = reservation.WaitlistEntry != null
            ? ToWaitlistDto(reservation.WaitlistEntry, company.GetWaitlistPosition(reservation.WaitlistEntry.Id))
            : null;

        return new CorporateReservationResultDto(
            bookingDto,
            waitlistDto,
            ToFraudAssessmentDto(reservation.FraudAssessment));
    }

    private static List<string> ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();
    }

    private static List<VehicleType> ParseVehicleTypes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<VehicleType>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Enum.TryParse<VehicleType>(s.Trim(), out var vehicleType) ? vehicleType : VehicleType.Car)
            .ToList();
    }
}

internal static class CorporateCommandHelpers
{
    public static BookingPolicy? CreateBookingPolicy(BookingPolicyDto? dto)
    {
        if (dto == null)
        {
            return null;
        }

        return BookingPolicy.Create(
            dto.MaxBookingsPerEmployeePerDay,
            dto.MaxBookingsPerEmployeePerWeek,
            dto.PriorityThreshold,
            dto.AllowedStartTime ?? new TimeSpan(7, 0, 0),
            dto.AllowedEndTime ?? new TimeSpan(22, 0, 0),
            dto.AllowWeekends);
    }

    public static Booking CreateEmployeeBooking(BookCorporateParkingCommand command, Guid parkingSpaceId, decimal amount)
    {
        return new Booking
        {
            UserId = command.UserId,
            ParkingSpaceId = parkingSpaceId,
            StartDateTime = command.Dto.StartDateTime,
            EndDateTime = command.Dto.EndDateTime,
            PricingType = PricingType.Hourly,
            VehicleType = command.Dto.VehicleType,
            VehicleNumber = command.Dto.VehicleNumber,
            BaseAmount = amount,
            TaxAmount = 0,
            ServiceFee = 0,
            DiscountAmount = 0,
            TotalAmount = amount,
            Status = BookingStatus.Confirmed,
            BookingReference = GenerateBookingReference("CORP"),
            QRCode = $"CORP-{Guid.NewGuid():N}".ToUpperInvariant()
        };
    }

    public static Booking CreateVisitorBooking(BookVisitorParkingCommand command, Guid parkingSpaceId, decimal amount)
    {
        return new Booking
        {
            UserId = command.UserId,
            ParkingSpaceId = parkingSpaceId,
            StartDateTime = command.Dto.StartDateTime,
            EndDateTime = command.Dto.EndDateTime,
            PricingType = PricingType.Hourly,
            VehicleType = VehicleType.Car,
            VehicleNumber = command.Dto.VisitorLicensePlate,
            BaseAmount = amount,
            TaxAmount = 0,
            ServiceFee = 0,
            DiscountAmount = 0,
            TotalAmount = amount,
            Status = BookingStatus.Confirmed,
            BookingReference = GenerateBookingReference("VIS")
        };
    }

    public static Booking CreateBookingFromWaitlist(CorporateWaitlistEntry waitlistEntry, Guid userId, Guid parkingSpaceId, decimal amount)
    {
        return new Booking
        {
            UserId = userId,
            ParkingSpaceId = parkingSpaceId,
            StartDateTime = waitlistEntry.RequestedStartDateTime,
            EndDateTime = waitlistEntry.RequestedEndDateTime,
            PricingType = PricingType.Hourly,
            VehicleType = waitlistEntry.VehicleType,
            VehicleNumber = waitlistEntry.IsVisitorBooking
                ? waitlistEntry.VisitorLicensePlate
                : waitlistEntry.VehicleNumber,
            BaseAmount = amount,
            TaxAmount = 0,
            ServiceFee = 0,
            DiscountAmount = 0,
            TotalAmount = amount,
            Status = BookingStatus.Confirmed,
            BookingReference = GenerateBookingReference(waitlistEntry.IsVisitorBooking ? "VIS" : "CORP"),
            QRCode = waitlistEntry.IsVisitorBooking ? null : $"CORP-{Guid.NewGuid():N}".ToUpperInvariant()
        };
    }

    public static string GenerateBookingReference(string prefix)
    {
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    public static DateOnly GetWeekStart(DateOnly date)
    {
        var diff = (7 + ((int)date.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff);
    }

    public static string BuildLockKey(Guid companyId, Guid allocationId, DateTime startUtc)
    {
        return $"lock:corp-booking:{companyId}:{allocationId}:{startUtc:yyyyMMddHH}";
    }
}
