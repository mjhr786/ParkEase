using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Bookings;

internal sealed class BookVisitorParkingHandler
    : ICommandHandler<BookVisitorParkingCommand, ApiResponse<CorporateReservationResultDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceBookingService _marketplaceBookings;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public BookVisitorParkingHandler(
        ICorporateUnitOfWork corporate,
        IMarketplaceBookingService marketplaceBookings,
        ICacheService cache,
        ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _marketplaceBookings = marketplaceBookings;
        _cache = cache;
        _quotaCache = quotaCache;
    }

    public async Task<ApiResponse<CorporateReservationResultDto>> HandleAsync(
        BookVisitorParkingCommand command,
        CancellationToken ct = default)
    {
        var quota = await _quotaCache.GetAllocationAsync(command.CompanyId, command.Dto.AllocationId, ct);
        if (quota is null)
            return new ApiResponse<CorporateReservationResultDto>(false, "Allocation not found.", null);
        if (!quota.IsBookable)
            return new ApiResponse<CorporateReservationResultDto>(false, "Active allocation not found.", null);

        var company = await _corporate.Companies.GetAggregateForBookingAsync(
            command.CompanyId,
            command.UserId,
            command.Dto.AllocationId,
            command.Dto.StartDateTime,
            command.Dto.EndDateTime,
            ct);
        if (company is null)
            return new ApiResponse<CorporateReservationResultDto>(false, "Company not found.", null);

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == command.Dto.AllocationId && !a.IsDeleted);
        if (allocation is null)
            return new ApiResponse<CorporateReservationResultDto>(false, "Allocation not found.", null);

        var membership = company.Memberships.FirstOrDefault(m => m.UserId == command.UserId && !m.IsDeleted);
        if (membership is null)
        {
            return new ApiResponse<CorporateReservationResultDto>(
                false, "You are not an active member of this company.", null);
        }

        var lockKey = CorporateCommandHelpers.BuildLockKey(
            command.CompanyId, allocation.Id, command.Dto.StartDateTime);
        if (!await _cache.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10), ct))
        {
            return new ApiResponse<CorporateReservationResultDto>(
                false,
                "System is processing other bookings for this allocation. Please try again in a few seconds.",
                null);
        }

        CorporateReservationOutcome? reservation = null;
        CorporateBookingDraft? draft = null;

        try
        {
            var usageDate = DateOnly.FromDateTime(command.Dto.StartDateTime);
            var weekStart = CorporateCommandHelpers.GetWeekStart(usageDate);
            var preCheck = await _corporate.CorporateBookings.GetReservationPreCheckAsync(
                command.CompanyId,
                membership.Id,
                allocation.Id,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                usageDate,
                weekStart,
                DateTime.UtcNow.AddHours(-24),
                DateTime.UtcNow.AddDays(-30),
                command.Dto.VisitorLicensePlate,
                ct);

            var duration = command.Dto.EndDateTime - command.Dto.StartDateTime;
            var amount = company.CalculateBookingAmount(quota.HourlyRate, duration);

            var stage = await _marketplaceBookings.StageCorporateBookingAsync(
                new StageCorporateBookingRequest(
                    command.UserId,
                    allocation.ParkingSpaceId,
                    command.Dto.StartDateTime,
                    command.Dto.EndDateTime,
                    amount,
                    command.Dto.VisitorLicensePlate,
                    IsVisitor: true),
                ct);

            draft = new CorporateBookingDraft(
                stage.BookingId,
                allocation.ParkingSpaceId,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                BookingStatus.Confirmed,
                VehicleType.Car,
                command.Dto.VisitorLicensePlate);

            var fraudAssessment = company.AssessFraudRisk(
                command.UserId,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                hasOverlappingMemberBooking: false,
                preCheck.HasOverlappingVehicleBooking,
                preCheck.RecentBookingCreateCount);

            reservation = company.ReserveVisitorParking(
                command.UserId,
                allocation.Id,
                draft,
                command.Dto.VisitorName,
                command.Dto.VisitorLicensePlate,
                command.Dto.AccessExpiry,
                preCheck.OccupiedSharedSlotNumbers,
                preCheck.SharedSlotUsageBySlot,
                preCheck.AnonymousOccupiedSharedBookings,
                fraudAssessment);

            await _corporate.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, ex.Message, null);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, ct);
        }

        if (reservation is { IsWaitlisted: false } && draft is not null)
        {
            await CacheInvalidation.ForBookingChangeAsync(
                _cache, draft.ParkingSpaceId, memberId: command.UserId, vendorId: null, ct);
            await _quotaCache.InvalidateCompanyAsync(command.CompanyId, ct);
        }

        var message = reservation!.IsWaitlisted
            ? "No shared slot is available right now. Added visitor request to waitlist."
            : "Visitor parking booked successfully.";

        return new ApiResponse<CorporateReservationResultDto>(
            true,
            message,
            CorporateMapping.ToReservationResultDto(reservation, company, draft));
    }
}
