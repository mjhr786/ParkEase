using ParkingApp.Application.Interfaces;

namespace ParkingApp.Application.Caching;

/// <summary>
/// Accurate, centralized cache invalidation for every critical mutation path.
/// Prefer these methods over ad-hoc Remove/RemoveByPattern so users never see stale data.
/// </summary>
public static class CacheInvalidation
{
    /// <summary>
    /// Parking space created/updated/deleted/toggled or media changed.
    /// Busts detail, discovery lists, owner list, and availability forecasts.
    /// </summary>
    public static Task ForParkingMutationAsync(
        ICacheService cache,
        Guid parkingSpaceId,
        Guid? ownerId = null,
        bool includeReviews = false,
        CancellationToken cancellationToken = default)
    {
        if (parkingSpaceId == Guid.Empty)
            return Task.CompletedTask;

        var tasks = new List<Task>(10)
        {
            cache.RemoveAsync(CacheKeys.Parking(parkingSpaceId), cancellationToken),
            cache.RemoveByPatternAsync(CacheKeys.SearchAll, cancellationToken),
            cache.RemoveByPatternAsync(CacheKeys.MapAll, cancellationToken),
            cache.RemoveByPatternAsync(CacheKeys.ParkingForecastAll, cancellationToken)
        };

        if (includeReviews)
            tasks.Add(cache.RemoveAsync(CacheKeys.Reviews(parkingSpaceId), cancellationToken));

        if (ownerId is { } vendorId && vendorId != Guid.Empty)
        {
            tasks.Add(cache.RemoveAsync(CacheKeys.VendorDashboard(vendorId), cancellationToken));
            tasks.Add(cache.RemoveAsync(CacheKeys.PendingRequestsCount(vendorId), cancellationToken));
            tasks.Add(cache.RemoveAsync(CacheKeys.OwnerParkings(vendorId), cancellationToken));
            tasks.Add(cache.RemoveByPatternAsync(CacheKeys.OwnerForecastAll, cancellationToken));
        }

        return Task.WhenAll(tasks);
    }

    /// <summary>
    /// Any booking lifecycle change (create, approve, reject, cancel, check-in/out, extension, reschedule, payment).
    /// Busts parking detail (embeds reservations), forecasts, and member/vendor dashboards.
    /// Does <b>not</b> bust global search/map by default — listing metadata is stable; AvailableSpots
    /// on search is denormalized and not recalculated per booking. Use includeDiscoveryLists for listing mutations.
    /// </summary>
    public static Task ForBookingChangeAsync(
        ICacheService cache,
        Guid parkingSpaceId,
        Guid? memberId = null,
        Guid? vendorId = null,
        CancellationToken cancellationToken = default,
        bool includeDiscoveryLists = false)
    {
        if (parkingSpaceId == Guid.Empty)
            return Task.CompletedTask;

        var tasks = new List<Task>(12)
        {
            cache.RemoveAsync(CacheKeys.Parking(parkingSpaceId), cancellationToken),
            // Space-level availability forecasts (namespace version bump — acceptable vs wrong occupancy)
            cache.RemoveByPatternAsync(CacheKeys.ParkingForecastAll, cancellationToken)
        };

        if (includeDiscoveryLists)
        {
            tasks.Add(cache.RemoveByPatternAsync(CacheKeys.SearchAll, cancellationToken));
            tasks.Add(cache.RemoveByPatternAsync(CacheKeys.MapAll, cancellationToken));
        }

        if (memberId is { } m && m != Guid.Empty)
            tasks.Add(cache.RemoveAsync(CacheKeys.MemberDashboard(m), cancellationToken));

        if (vendorId is { } v && v != Guid.Empty)
        {
            tasks.Add(cache.RemoveAsync(CacheKeys.VendorDashboard(v), cancellationToken));
            tasks.Add(cache.RemoveAsync(CacheKeys.PendingRequestsCount(v), cancellationToken));
            tasks.Add(cache.RemoveAsync(CacheKeys.OwnerParkings(v), cancellationToken));
            tasks.Add(cache.RemoveByPatternAsync(CacheKeys.OwnerForecastAll, cancellationToken));
        }
        // When vendor unknown: do not bump all owner forecasts (was global thrash).
        // Callers that care should pass vendorId (event handlers already resolve owner).

        return Task.WhenAll(tasks);
    }

    /// <summary>
    /// Review create/update/delete/owner-response — ratings appear on parking detail and discovery.
    /// </summary>
    public static Task ForReviewChangeAsync(
        ICacheService cache,
        Guid parkingSpaceId,
        Guid? ownerId = null,
        CancellationToken cancellationToken = default)
    {
        if (parkingSpaceId == Guid.Empty)
            return Task.CompletedTask;

        var tasks = new List<Task>(8)
        {
            cache.RemoveAsync(CacheKeys.Reviews(parkingSpaceId), cancellationToken),
            cache.RemoveAsync(CacheKeys.Parking(parkingSpaceId), cancellationToken),
            cache.RemoveByPatternAsync(CacheKeys.SearchAll, cancellationToken),
            cache.RemoveByPatternAsync(CacheKeys.MapAll, cancellationToken)
        };

        if (ownerId is { } v && v != Guid.Empty)
        {
            tasks.Add(cache.RemoveAsync(CacheKeys.VendorDashboard(v), cancellationToken));
            tasks.Add(cache.RemoveAsync(CacheKeys.OwnerParkings(v), cancellationToken));
        }

        return Task.WhenAll(tasks);
    }

    /// <summary>Profile updates that affect the current-user cache.</summary>
    public static Task ForUserChangeAsync(
        ICacheService cache,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return Task.CompletedTask;

        return cache.RemoveAsync(CacheKeys.User(userId), cancellationToken);
    }

    /// <summary>Parking pass create/assign — pricing depends on active passes.</summary>
    public static Task ForUserPassesAsync(
        ICacheService cache,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return Task.CompletedTask;

        return cache.RemoveAsync(CacheKeys.UserActivePasses(userId), cancellationToken);
    }

    public static Task ForUserPassesAsync(
        ICacheService cache,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return Task.CompletedTask;

        return Task.WhenAll(ids.Select(id => cache.RemoveAsync(CacheKeys.UserActivePasses(id), cancellationToken)));
    }

    /// <summary>Company dashboard aggregates after bookings / allocations / members change.</summary>
    public static Task ForCompanyDashboardAsync(
        ICacheService cache,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return Task.CompletedTask;

        return cache.RemoveAsync(CacheKeys.CompanyDashboard(companyId), cancellationToken);
    }

    /// <summary>Discovery-only bust (e.g. brand-new listing with no detail key yet).</summary>
    public static Task ForDiscoveryListsAsync(
        ICacheService cache,
        CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            cache.RemoveByPatternAsync(CacheKeys.SearchAll, cancellationToken),
            cache.RemoveByPatternAsync(CacheKeys.MapAll, cancellationToken));
}
