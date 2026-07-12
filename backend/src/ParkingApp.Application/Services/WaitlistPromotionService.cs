using Microsoft.Extensions.Logging;
using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Marketplace;

namespace ParkingApp.Application.Services;

public sealed class WaitlistPromotionService : IWaitlistPromotionService
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;
    private readonly IWaitlistPromotionStore _store;
    private readonly ILogger<WaitlistPromotionService> _logger;

    public WaitlistPromotionService(
        ICorporateUnitOfWork corporate,
        IMarketplaceUnitOfWork marketplace,
        ICacheService cache,
        ICompanyQuotaCache quotaCache,
        IWaitlistPromotionStore store,
        ILogger<WaitlistPromotionService> logger)
    {
        _corporate = corporate;
        _marketplace = marketplace;
        _cache = cache;
        _quotaCache = quotaCache;
        _store = store;
        _logger = logger;
    }

    public async Task<ApiResponse<CorporateReservationResultDto>> PromoteAsync(
        Guid companyId,
        Guid waitlistEntryId,
        Guid? adminUserId,
        CancellationToken cancellationToken = default)
    {
        // Slim aggregate: no full company graph (was GetFullAsync).
        var company = await _corporate.Companies.GetAggregateForWaitlistPromotionAsync(
            companyId,
            waitlistEntryId,
            adminUserId,
            cancellationToken);
        if (company == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Company not found.", null);
        }

        if (adminUserId.HasValue)
        {
            var adminMembership = company.Memberships.FirstOrDefault(m => m.UserId == adminUserId.Value && !m.IsDeleted);
            if (adminMembership == null || !adminMembership.IsActive || !adminMembership.IsAdmin)
            {
                return new ApiResponse<CorporateReservationResultDto>(false, "Only company admins can promote waitlist entries.", null);
            }
        }

        var waitlistEntry = company.WaitlistEntries.FirstOrDefault(w => w.Id == waitlistEntryId && !w.IsDeleted);
        if (waitlistEntry == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Waitlist entry not found.", null);
        }

        if (waitlistEntry.Status != WaitlistStatus.Pending)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Only pending waitlist entries can be promoted.", null);
        }

        if (waitlistEntry.RequestedEndDateTime <= DateTime.UtcNow)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Waitlist window has already ended.", null);
        }

        var targetMembership = company.Memberships.FirstOrDefault(m => m.Id == waitlistEntry.MembershipId && !m.IsDeleted);
        if (targetMembership == null || !targetMembership.IsActive)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Waitlist member is no longer active.", null);
        }

        var quota = await _quotaCache.GetAllocationAsync(companyId, waitlistEntry.AllocationId, cancellationToken);
        if (quota == null)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Allocation not found.", null);
        }

        if (!quota.IsBookable)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Active allocation not found.", null);
        }

        var lockKey = CorporateCommandHelpers.BuildLockKey(companyId, waitlistEntry.AllocationId, waitlistEntry.RequestedStartDateTime);
        if (!await _cache.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10), cancellationToken))
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
            var allocation = company.Allocations.FirstOrDefault(a => a.Id == waitlistEntry.AllocationId && !a.IsDeleted);
            if (allocation == null)
            {
                return new ApiResponse<CorporateReservationResultDto>(false, "Allocation not found.", null);
            }

            var usageDate = DateOnly.FromDateTime(waitlistEntry.RequestedStartDateTime);
            var weekStart = CorporateCommandHelpers.GetWeekStart(usageDate);
            var vehicleNumber = waitlistEntry.IsVisitorBooking
                ? waitlistEntry.VisitorLicensePlate
                : waitlistEntry.VehicleNumber;

            var preCheck = await _corporate.CorporateBookings.GetReservationPreCheckAsync(
                companyId,
                targetMembership.Id,
                allocation.Id,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                usageDate,
                weekStart,
                DateTime.UtcNow.AddHours(-24),
                DateTime.UtcNow.AddDays(-30),
                vehicleNumber,
                cancellationToken);

            var duration = waitlistEntry.RequestedEndDateTime - waitlistEntry.RequestedStartDateTime;
            var amount = company.CalculateBookingAmount(quota.HourlyRate, duration);
            booking = CorporateCommandHelpers.CreateBookingFromWaitlist(waitlistEntry, targetMembership.UserId, quota.ParkingSpaceId, amount);

            var fraudAssessment = company.AssessFraudRisk(
                targetMembership.UserId,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                preCheck.HasOverlappingMemberBooking,
                preCheck.HasOverlappingVehicleBooking,
                preCheck.RecentBookingCreateCount);

            if (waitlistEntry.IsVisitorBooking)
            {
                reservation = company.ReserveVisitorParking(
                    targetMembership.UserId,
                    allocation.Id,
                    booking,
                    waitlistEntry.VisitorName ?? string.Empty,
                    waitlistEntry.VisitorLicensePlate ?? string.Empty,
                    waitlistEntry.AccessExpiryUtc ?? waitlistEntry.RequestedEndDateTime,
                    preCheck.OccupiedSharedSlotNumbers,
                    preCheck.SharedSlotUsageBySlot,
                    preCheck.AnonymousOccupiedSharedBookings,
                    fraudAssessment);
            }
            else
            {
                reservation = company.ReserveEmployeeParking(
                    targetMembership.UserId,
                    allocation.Id,
                    booking,
                    preCheck.DayBookingCount,
                    preCheck.WeekBookingCount,
                    preCheck.OccupiedSharedSlotNumbers,
                    preCheck.SharedSlotUsageBySlot,
                    preCheck.AnonymousOccupiedSharedBookings,
                    fraudAssessment);
            }

            if (reservation.IsWaitlisted)
            {
                return new ApiResponse<CorporateReservationResultDto>(
                    false,
                    "This waitlist entry cannot be promoted yet. It may not be first in line or no shared slot is available.",
                    CorporateMapping.ToReservationResultDto(reservation, booking, company));
            }

            await _marketplace.Bookings.AddAsync(booking, cancellationToken);
            if (waitlistEntry.IsVisitorBooking)
            {
                booking.SetQrCode(reservation.Booking!.AccessPolicy?.QrCodeToken);
            }

            await _corporate.SaveChangesAsync(cancellationToken);

            await CacheInvalidation.ForBookingChangeAsync(
                _cache,
                booking.ParkingSpaceId,
                memberId: booking.UserId,
                vendorId: null,
                cancellationToken);
            await _quotaCache.InvalidateCompanyAsync(companyId, cancellationToken);

            _logger.LogInformation(
                "Waitlist entry {WaitlistEntryId} promoted for company {CompanyId} ({Mode})",
                waitlistEntryId,
                companyId,
                adminUserId.HasValue ? "admin" : "auto");
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, ex.Message, null);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, cancellationToken);
        }

        return new ApiResponse<CorporateReservationResultDto>(
            true,
            adminUserId.HasValue
                ? "Waitlist entry promoted to a confirmed corporate booking."
                : "Waitlist entry auto-promoted to a confirmed corporate booking.",
            CorporateMapping.ToReservationResultDto(reservation!, booking, company!));
    }

    public async Task<WaitlistAutoPromotionBatchResult> ProcessPendingAsync(
        int batchSize = 25,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var take = Math.Clamp(batchSize, 1, 100);

        var expired = await _store.ExpireStalePendingAsync(utcNow, cancellationToken);
        if (expired > 0)
        {
            _logger.LogInformation("Expired {Count} stale corporate waitlist entr(y/ies)", expired);
        }

        var candidates = await _store.GetPromotionCandidatesAsync(utcNow, take, cancellationToken);
        var promoted = 0;
        var skipped = 0;
        var attempted = 0;

        foreach (var candidate in candidates)
        {
            attempted++;
            try
            {
                var result = await PromoteAsync(
                    candidate.CompanyId,
                    candidate.WaitlistEntryId,
                    adminUserId: null,
                    cancellationToken);

                if (result.Success)
                {
                    promoted++;
                }
                else
                {
                    skipped++;
                    _logger.LogDebug(
                        "Auto-promote skipped {WaitlistEntryId}: {Message}",
                        candidate.WaitlistEntryId,
                        result.Message);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped++;
                _logger.LogWarning(
                    ex,
                    "Auto-promote failed for waitlist entry {WaitlistEntryId} company {CompanyId}",
                    candidate.WaitlistEntryId,
                    candidate.CompanyId);
            }
        }

        return new WaitlistAutoPromotionBatchResult(promoted, expired, attempted, skipped);
    }
}
