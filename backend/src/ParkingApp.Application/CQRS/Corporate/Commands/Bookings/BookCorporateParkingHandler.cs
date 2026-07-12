using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Bookings;

public class BookCorporateParkingHandler : ICommandHandler<BookCorporateParkingCommand, ApiResponse<CorporateReservationResultDto>>
{
    private readonly ICorporateUnitOfWork _corporate;
    private readonly IMarketplaceUnitOfWork _marketplace;
    private readonly ICacheService _cache;
    private readonly ICompanyQuotaCache _quotaCache;

    public BookCorporateParkingHandler(ICorporateUnitOfWork corporate, IMarketplaceUnitOfWork marketplace, ICacheService cache, ICompanyQuotaCache quotaCache)
    {
        _corporate = corporate;
        _marketplace = marketplace;
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
        var company = await _corporate.Companies.GetAggregateForBookingAsync(
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
            var dayCount = await _corporate.CorporateBookings.GetMembershipBookingCountForDateAsync(command.CompanyId, membership.Id, usageDate, ct);
            var weekCount = await _corporate.CorporateBookings.GetMembershipBookingCountForWeekAsync(command.CompanyId, membership.Id, weekStart, ct);
            var activeSharedCount = await _corporate.CorporateBookings.GetActiveSharedBookingsCountAsync(
                command.CompanyId,
                allocation.Id,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                ct);
            var occupiedSharedSlotNumbers = await _corporate.CorporateBookings.GetOccupiedSharedSlotNumbersAsync(
                command.CompanyId,
                allocation.Id,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                ct);
            var sharedSlotUsageBySlot = await _corporate.CorporateBookings.GetSharedSlotUsageCountsAsync(
                command.CompanyId,
                allocation.Id,
                DateTime.UtcNow.AddDays(-30),
                ct);
            var anonymousOccupiedSharedBookings = Math.Max(0, activeSharedCount - occupiedSharedSlotNumbers.Count);
            var hasOverlappingBooking = await _corporate.CorporateBookings.HasOverlappingBookingAsync(
                command.CompanyId,
                membership.Id,
                command.Dto.StartDateTime,
                command.Dto.EndDateTime,
                ct);
            var hasOverlappingVehicleBooking = !string.IsNullOrWhiteSpace(command.Dto.VehicleNumber)
                && await _corporate.CorporateBookings.HasOverlappingVehicleBookingAsync(
                    command.CompanyId,
                    allocation.Id,
                    command.Dto.VehicleNumber!,
                    command.Dto.StartDateTime,
                    command.Dto.EndDateTime,
                    ct);
            var recentBookingCreations = await _corporate.CorporateBookings.GetRecentBookingCreateCountAsync(
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
                await _marketplace.Bookings.AddAsync(booking, ct);
            }

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

        var message = reservation!.IsWaitlisted
            ? "No shared slot is available right now. Added to waitlist."
            : "Corporate parking booked successfully.";

        return new ApiResponse<CorporateReservationResultDto>(
            true,
            message,
            CorporateMapping.ToReservationResultDto(reservation, booking, company));
    }
}
