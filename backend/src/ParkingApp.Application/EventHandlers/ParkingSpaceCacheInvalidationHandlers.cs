using Microsoft.Extensions.Logging;
using ParkingApp.Application.Caching;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Events.Parking;

namespace ParkingApp.Application.EventHandlers;

/// <summary>
/// Invalidates parking detail, discovery (search/map), forecasts, and owner dashboards
/// when a parking space is created, updated, deleted, or toggled.
/// </summary>
public sealed class ParkingSpaceCreatedCacheHandler : IDomainEventHandler<ParkingSpaceCreatedEvent>
{
    private readonly ICacheService _cache;
    private readonly ILogger<ParkingSpaceCreatedCacheHandler> _logger;

    public ParkingSpaceCreatedCacheHandler(ICacheService cache, ILogger<ParkingSpaceCreatedCacheHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task HandleAsync(ParkingSpaceCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        await CacheInvalidation.ForParkingMutationAsync(
            _cache,
            domainEvent.ParkingSpaceId,
            ownerId: domainEvent.OwnerId,
            includeReviews: false,
            cancellationToken);

        _logger.LogDebug("Cache invalidated after parking space {ParkingSpaceId} created", domainEvent.ParkingSpaceId);
    }
}

public sealed class ParkingSpaceUpdatedCacheHandler : IDomainEventHandler<ParkingSpaceUpdatedEvent>
{
    private readonly ICacheService _cache;
    private readonly ILogger<ParkingSpaceUpdatedCacheHandler> _logger;

    public ParkingSpaceUpdatedCacheHandler(ICacheService cache, ILogger<ParkingSpaceUpdatedCacheHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task HandleAsync(ParkingSpaceUpdatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        await CacheInvalidation.ForParkingMutationAsync(
            _cache,
            domainEvent.ParkingSpaceId,
            ownerId: null,
            includeReviews: false,
            cancellationToken);

        _logger.LogDebug("Cache invalidated after parking space {ParkingSpaceId} updated", domainEvent.ParkingSpaceId);
    }
}

public sealed class ParkingSpaceDeletedCacheHandler : IDomainEventHandler<ParkingSpaceDeletedEvent>
{
    private readonly ICacheService _cache;
    private readonly ILogger<ParkingSpaceDeletedCacheHandler> _logger;

    public ParkingSpaceDeletedCacheHandler(ICacheService cache, ILogger<ParkingSpaceDeletedCacheHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task HandleAsync(ParkingSpaceDeletedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        await CacheInvalidation.ForParkingMutationAsync(
            _cache,
            domainEvent.ParkingSpaceId,
            ownerId: domainEvent.OwnerId,
            includeReviews: true,
            cancellationToken);

        _logger.LogDebug("Cache invalidated after parking space {ParkingSpaceId} deleted", domainEvent.ParkingSpaceId);
    }
}

public sealed class ParkingSpaceToggledCacheHandler : IDomainEventHandler<ParkingSpaceToggledEvent>
{
    private readonly ICacheService _cache;
    private readonly ILogger<ParkingSpaceToggledCacheHandler> _logger;

    public ParkingSpaceToggledCacheHandler(ICacheService cache, ILogger<ParkingSpaceToggledCacheHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task HandleAsync(ParkingSpaceToggledEvent domainEvent, CancellationToken cancellationToken = default)
    {
        await CacheInvalidation.ForParkingMutationAsync(
            _cache,
            domainEvent.ParkingSpaceId,
            ownerId: null,
            includeReviews: false,
            cancellationToken);

        _logger.LogDebug(
            "Cache invalidated after parking space {ParkingSpaceId} toggled to {IsActive}",
            domainEvent.ParkingSpaceId,
            domainEvent.IsActive);
    }
}
