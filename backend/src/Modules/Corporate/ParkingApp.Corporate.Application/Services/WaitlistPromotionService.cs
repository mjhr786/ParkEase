using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Corporate.Application.Services;

internal sealed class WaitlistPromotionService : IWaitlistPromotionService
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IWaitlistPromotionStore _store;
    private readonly IMarketplaceBookingService _marketplaceBookingService;
    private readonly ICacheService _cache;
    private readonly ILogger<WaitlistPromotionService> _logger;

    public WaitlistPromotionService(
        ICorporateUnitOfWork corporate,
        IWaitlistPromotionStore store,
        IMarketplaceBookingService marketplaceBookingService,
        ICacheService cache,
        ILogger<WaitlistPromotionService> logger)
    {
        _corporate = corporate;
        _store = store;
        _marketplaceBookingService = marketplaceBookingService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<CorporateReservationResultDto>> PromoteAsync(
        Guid companyId,
        Guid waitlistEntryId,
        Guid? adminUserId,
        CancellationToken cancellationToken = default)
    {
        var company = await _corporate.Companies.GetAggregateForWaitlistPromotionAsync(companyId, waitlistEntryId, adminUserId, cancellationToken);
        if (company == null || !company.IsActive)
            return new ApiResponse<CorporateReservationResultDto>(false, "Company not found or inactive.", null!);

        var waitlistEntry = company.WaitlistEntries.FirstOrDefault(w => w.Id == waitlistEntryId);
        if (waitlistEntry == null || waitlistEntry.CompanyId != companyId || waitlistEntry.Status != ParkingApp.Domain.Enums.WaitlistStatus.Pending)
            return new ApiResponse<CorporateReservationResultDto>(false, "Waitlist entry not found or not pending.", null!);

        var targetMembership = company.Memberships.FirstOrDefault(m => m.Id == waitlistEntry.MembershipId);
        if (targetMembership == null || !targetMembership.IsActive)
            return new ApiResponse<CorporateReservationResultDto>(false, "Employee membership is no longer active.", null!);

        var allocation = company.Allocations.FirstOrDefault(a => a.Id == waitlistEntry.AllocationId);
        if (allocation == null || !allocation.IsActiveAllocation)
            return new ApiResponse<CorporateReservationResultDto>(false, "Allocation no longer valid.", null!);

        var lockKey = $"lock:corp-booking:{companyId}:{allocation.Id}:{waitlistEntry.RequestedStartDateTime:yyyyMMddHH}";
        var acquired = await _cache.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10), cancellationToken);
        if (!acquired)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, "Could not acquire lock for promotion. Try again.", null!);
        }

        CorporateReservationOutcome? reservation = null;

        try
        {
            var activeSharedCount = await _corporate.CorporateBookings.GetActiveSharedBookingsCountAsync(
                companyId,
                allocation.Id,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                cancellationToken);
            var occupiedSharedSlotNumbers = await _corporate.CorporateBookings.GetOccupiedSharedSlotNumbersAsync(
                companyId,
                allocation.Id,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                cancellationToken);
            var sharedSlotUsageBySlot = await _corporate.CorporateBookings.GetSharedSlotUsageCountsAsync(
                companyId,
                allocation.Id,
                DateTime.UtcNow.AddDays(-30),
                cancellationToken);
            var anonymousOccupiedSharedBookings = Math.Max(0, activeSharedCount - occupiedSharedSlotNumbers.Count);
            var recentBookingCreations = await _corporate.CorporateBookings.GetRecentBookingCreateCountAsync(
                companyId,
                targetMembership.Id,
                DateTime.UtcNow.AddHours(-24),
                cancellationToken);

            var duration = waitlistEntry.RequestedEndDateTime - waitlistEntry.RequestedStartDateTime;
            var amount = company.CalculateBookingAmount(0m, duration); // Quotas are removed, rate is 0

            var hasOverlappingBooking = await _corporate.CorporateBookings.HasOverlappingBookingAsync(
                companyId,
                targetMembership.Id,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                cancellationToken);
            var vehicleNumber = waitlistEntry.IsVisitorBooking ? waitlistEntry.VisitorLicensePlate : waitlistEntry.VehicleNumber;
            var hasOverlappingVehicleBooking = !string.IsNullOrWhiteSpace(vehicleNumber)
                && await _corporate.CorporateBookings.HasOverlappingVehicleBookingAsync(
                    companyId,
                    allocation.Id,
                    vehicleNumber,
                    waitlistEntry.RequestedStartDateTime,
                    waitlistEntry.RequestedEndDateTime,
                    cancellationToken);

            var fraudAssessment = company.AssessFraudRisk(
                targetMembership.UserId,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                hasOverlappingBooking,
                hasOverlappingVehicleBooking,
                recentBookingCreations);

            var stageResult = await _marketplaceBookingService.StageCorporateBookingAsync(new StageCorporateBookingRequest(
                targetMembership.UserId,
                allocation.ParkingSpaceId,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                amount,
                vehicleNumber,
                waitlistEntry.IsVisitorBooking
            ), cancellationToken);

            var draft = new CorporateBookingDraft(
                stageResult.BookingId,
                allocation.ParkingSpaceId,
                waitlistEntry.RequestedStartDateTime,
                waitlistEntry.RequestedEndDateTime,
                BookingStatus.Confirmed,
                waitlistEntry.IsVisitorBooking ? VehicleType.Car : waitlistEntry.VehicleType,
                vehicleNumber
            );

            if (waitlistEntry.IsVisitorBooking)
            {
                reservation = company.ReserveVisitorParking(
                    targetMembership.UserId,
                    allocation.Id,
                    draft,
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
                var diff = (7 + ((int)usageDate.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
                var weekStart = usageDate.AddDays(-diff);
                var dayCount = await _corporate.CorporateBookings.GetMembershipBookingCountForDateAsync(companyId, targetMembership.Id, usageDate, cancellationToken);
                var weekCount = await _corporate.CorporateBookings.GetMembershipBookingCountForWeekAsync(companyId, targetMembership.Id, weekStart, cancellationToken);

                reservation = company.ReserveEmployeeParking(
                    targetMembership.UserId,
                    allocation.Id,
                    draft,
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
                    CorporateMapping.ToReservationResultDto(reservation, company, draft));
            }

            await _corporate.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Waitlist entry {WaitlistEntryId} promoted for company {CompanyId} ({Mode})",
                waitlistEntryId,
                companyId,
                adminUserId.HasValue ? "admin" : "auto");

            return new ApiResponse<CorporateReservationResultDto>(
                true,
                adminUserId.HasValue
                    ? "Waitlist entry promoted to a confirmed corporate booking."
                    : "Waitlist entry auto-promoted to a confirmed corporate booking.",
                CorporateMapping.ToReservationResultDto(reservation, company, draft));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CorporateReservationResultDto>(false, ex.Message, null!);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, cancellationToken);
        }
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
