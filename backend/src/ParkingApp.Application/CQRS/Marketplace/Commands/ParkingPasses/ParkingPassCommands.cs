using ParkingApp.Application.Caching;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.ValueObjects;

namespace ParkingApp.Application.CQRS.Commands.ParkingPasses;

public sealed record CreateParkingPassCommand(Guid RequestedByUserId, CreateParkingPassDto Dto)
    : ICommand<ApiResponse<ParkingPassDto>>;

public sealed record AssignCorporatePassCommand(Guid RequestedByUserId, AssignCorporatePassDto Dto)
    : ICommand<ApiResponse<CorporatePassAssignmentResultDto>>;

public sealed class CreateParkingPassHandler : ICommandHandler<CreateParkingPassCommand, ApiResponse<ParkingPassDto>>
{
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly IIdentityUnitOfWork _identity;
    private readonly ICacheService _cache;

    public CreateParkingPassHandler(IMarketplaceUnitOfWork marketplace, IIdentityUnitOfWork identity, ICacheService cache)
    {
        _marketplace = marketplace;
        _identity = identity;
        _cache = cache;
    }

    public async Task<ApiResponse<ParkingPassDto>> HandleAsync(CreateParkingPassCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Dto.PassType == PassTypeKind.Corporate)
        {
            return new ApiResponse<ParkingPassDto>(false, "Corporate passes must be allocated through the corporate endpoint.", null);
        }

        var user = await _identity.Users.GetByIdAsync(command.RequestedByUserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return new ApiResponse<ParkingPassDto>(false, "User account is not active.", null);
        }

        var (parkingSpace, parkingZoneCode, coverageError) = await ResolveCoverageAsync(
            command.Dto.ParkingSpaceId,
            command.Dto.ParkingZoneCode,
            cancellationToken);

        if (coverageError != null)
        {
            return new ApiResponse<ParkingPassDto>(false, coverageError, null);
        }

        var pass = ParkingPass.Create(
            command.RequestedByUserId,
            PassType.From(command.Dto.PassType),
            Duration.Create(command.Dto.StartDateUtc, command.Dto.EndDateUtc),
            BuildUsagePolicy(command.Dto.UsageMode, command.Dto.DailyHourLimit),
            command.Dto.DiscountPercentage,
            parkingSpace?.Id,
            parkingZoneCode,
            command.RequestedByUserId);

        await _marketplace.ParkingPasses.AddAsync(pass, cancellationToken);
        await _marketplace.SaveChangesAsync(cancellationToken);
        await CacheInvalidation.ForUserPassesAsync(_cache, command.RequestedByUserId, cancellationToken);

        var createdPass = await _marketplace.ParkingPasses.GetByIdAsync(pass.Id, cancellationToken) ?? pass;
        return new ApiResponse<ParkingPassDto>(true, "Parking pass created successfully.", createdPass.ToDto());
    }

    private async Task<(ParkingSpace? ParkingSpace, string? ParkingZoneCode, string? Error)> ResolveCoverageAsync(
        Guid? parkingSpaceId,
        string? parkingZoneCode,
        CancellationToken cancellationToken)
    {
        if (parkingSpaceId.HasValue)
        {
            var parkingSpace = await _marketplace.ParkingSpaces.GetByIdAsync(parkingSpaceId.Value, cancellationToken);
            if (parkingSpace == null || !parkingSpace.IsActive)
            {
                return (null, null, "The selected parking space does not exist or is not active.");
            }

            return (parkingSpace, null, null);
        }

        var normalizedZoneCode = NormalizeZoneCode(parkingZoneCode);
        var zoneExists = await _marketplace.ParkingSpaces.ExistsWithZoneCodeAsync(normalizedZoneCode!, cancellationToken);

        if (!zoneExists)
        {
            return (null, null, "The selected parking zone does not exist.");
        }

        return (null, normalizedZoneCode, null);
    }

    private static UsagePolicy BuildUsagePolicy(PassUsageMode usageMode, int? dailyHourLimit)
    {
        return usageMode switch
        {
            PassUsageMode.UnlimitedEntries => UsagePolicy.UnlimitedEntries(),
            PassUsageMode.LimitedHoursPerDay => UsagePolicy.LimitedHoursPerDay(dailyHourLimit ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(usageMode), "Unsupported parking pass usage mode.")
        };
    }

    private static string? NormalizeZoneCode(string? parkingZoneCode)
    {
        return string.IsNullOrWhiteSpace(parkingZoneCode)
            ? null
            : parkingZoneCode.Trim().ToUpperInvariant();
    }
}

public sealed class AssignCorporatePassHandler : ICommandHandler<AssignCorporatePassCommand, ApiResponse<CorporatePassAssignmentResultDto>>
{
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly IIdentityUnitOfWork _identity;
    private readonly ICacheService _cache;

    public AssignCorporatePassHandler(IMarketplaceUnitOfWork marketplace, IIdentityUnitOfWork identity, ICacheService cache)
    {
        _marketplace = marketplace;
        _identity = identity;
        _cache = cache;
    }

    public async Task<ApiResponse<CorporatePassAssignmentResultDto>> HandleAsync(AssignCorporatePassCommand command, CancellationToken cancellationToken = default)
    {
        var administrator = await _identity.Users.GetByIdAsync(command.RequestedByUserId, cancellationToken);
        if (administrator == null || !administrator.IsActive || administrator.Role != UserRole.Admin)
        {
            return new ApiResponse<CorporatePassAssignmentResultDto>(false, "Only administrators can assign corporate passes.", null);
        }

        var employeeIds = command.Dto.EmployeeUserIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();

        if (employeeIds.Count == 0)
        {
            return new ApiResponse<CorporatePassAssignmentResultDto>(false, "At least one employee must be selected.", null);
        }

        var employees = (await _identity.Users.FindAsync(user => employeeIds.Contains(user.Id) && user.IsActive, cancellationToken)).ToList();
        if (employees.Count != employeeIds.Count)
        {
            return new ApiResponse<CorporatePassAssignmentResultDto>(false, "One or more selected employees do not exist or are inactive.", null);
        }

        var (parkingSpace, parkingZoneCode, coverageError) = await ResolveCoverageAsync(
            command.Dto.ParkingSpaceId,
            command.Dto.ParkingZoneCode,
            cancellationToken);

        if (coverageError != null)
        {
            return new ApiResponse<CorporatePassAssignmentResultDto>(false, coverageError, null);
        }

        var duration = Duration.Create(command.Dto.StartDateUtc, command.Dto.EndDateUtc);
        var usagePolicy = BuildUsagePolicy(command.Dto.UsageMode, command.Dto.DailyHourLimit);
        var batchReference = string.IsNullOrWhiteSpace(command.Dto.CorporateBatchReference)
            ? $"CORP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"
            : command.Dto.CorporateBatchReference.Trim().ToUpperInvariant();

        var corporatePasses = employees
            .Select(employee => ParkingPass.Create(
                employee.Id,
                PassType.Corporate(),
                duration,
                usagePolicy,
                command.Dto.DiscountPercentage,
                parkingSpace?.Id,
                parkingZoneCode,
                command.RequestedByUserId,
                batchReference))
            .ToList();

        await _marketplace.ParkingPasses.AddRangeAsync(corporatePasses, cancellationToken);
        await _marketplace.SaveChangesAsync(cancellationToken);
        await CacheInvalidation.ForUserPassesAsync(_cache, employeeIds, cancellationToken);

        var employeeLookup = employees.ToDictionary(employee => employee.Id);
        var now = DateTime.UtcNow;
        var passDtos = corporatePasses
            .Select(pass => new ParkingPassDto(
                pass.Id,
                pass.UserId,
                employeeLookup[pass.UserId].FullName,
                PassTypeKind.Corporate,
                pass.Duration.StartDateUtc,
                pass.Duration.EndDateUtc,
                pass.CoverageType,
                pass.ParkingSpaceId,
                parkingSpace?.Title,
                pass.ParkingZoneCode,
                pass.UsagePolicy.Mode,
                pass.UsagePolicy.DailyHourLimit,
                pass.DiscountPercentage,
                pass.GetState(now),
                pass.IsActiveOn(now),
                pass.IsExpiredOn(now),
                pass.AllocatedByUserId,
                pass.CorporateBatchReference,
                pass.CreatedAt))
            .ToList();

        var result = new CorporatePassAssignmentResultDto(batchReference, passDtos.Count, passDtos);
        return new ApiResponse<CorporatePassAssignmentResultDto>(true, "Corporate passes assigned successfully.", result);
    }

    private async Task<(ParkingSpace? ParkingSpace, string? ParkingZoneCode, string? Error)> ResolveCoverageAsync(
        Guid? parkingSpaceId,
        string? parkingZoneCode,
        CancellationToken cancellationToken)
    {
        if (parkingSpaceId.HasValue)
        {
            var parkingSpace = await _marketplace.ParkingSpaces.GetByIdAsync(parkingSpaceId.Value, cancellationToken);
            if (parkingSpace == null || !parkingSpace.IsActive)
            {
                return (null, null, "The selected parking space does not exist or is not active.");
            }

            return (parkingSpace, null, null);
        }

        var normalizedZoneCode = NormalizeZoneCode(parkingZoneCode);
        var zoneExists = await _marketplace.ParkingSpaces.ExistsWithZoneCodeAsync(normalizedZoneCode!, cancellationToken);

        if (!zoneExists)
        {
            return (null, null, "The selected parking zone does not exist.");
        }

        return (null, normalizedZoneCode, null);
    }

    private static UsagePolicy BuildUsagePolicy(PassUsageMode usageMode, int? dailyHourLimit)
    {
        return usageMode switch
        {
            PassUsageMode.UnlimitedEntries => UsagePolicy.UnlimitedEntries(),
            PassUsageMode.LimitedHoursPerDay => UsagePolicy.LimitedHoursPerDay(dailyHourLimit ?? 0),
            _ => throw new ArgumentOutOfRangeException(nameof(usageMode), "Unsupported parking pass usage mode.")
        };
    }

    private static string? NormalizeZoneCode(string? parkingZoneCode)
    {
        return string.IsNullOrWhiteSpace(parkingZoneCode)
            ? null
            : parkingZoneCode.Trim().ToUpperInvariant();
    }
}
