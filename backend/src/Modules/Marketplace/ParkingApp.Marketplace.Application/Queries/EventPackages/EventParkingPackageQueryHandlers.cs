using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.EventPackages;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Queries.EventPackages;

internal sealed class GetEventPackagesForParkingHandler
    : IQueryHandler<GetEventPackagesForParkingQuery, ApiResponse<List<EventParkingPackageDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetEventPackagesForParkingHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<List<EventParkingPackageDto>>> HandleAsync(
        GetEventPackagesForParkingQuery query,
        CancellationToken cancellationToken = default)
    {
        var packages = await _unitOfWork.EventParkingPackages.GetByParkingSpaceIdAsync(
            query.ParkingSpaceId,
            query.ActiveOnly,
            cancellationToken);

        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(query.ParkingSpaceId, cancellationToken);
        var now = DateTime.UtcNow;
        var list = packages.Select(p =>
        {
            var dto = EventPackageMapper.ToDto(p, now);
            if (parking is not null && string.Equals(dto.ParkingSpaceTitle, "Parking", StringComparison.Ordinal))
            {
                dto = dto with
                {
                    ParkingSpaceTitle = parking.Title,
                    ParkingSpaceAddress = parking.Address,
                    ParkingSpaceCity = parking.City
                };
            }

            return dto;
        }).ToList();

        return new ApiResponse<List<EventParkingPackageDto>>(true, null, list);
    }
}

internal sealed class GetOnSaleEventPackagesHandler
    : IQueryHandler<GetOnSaleEventPackagesQuery, ApiResponse<List<EventParkingPackageDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetOnSaleEventPackagesHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<List<EventParkingPackageDto>>> HandleAsync(
        GetOnSaleEventPackagesQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var packages = await _unitOfWork.EventParkingPackages.GetOnSaleAsync(now, query.Take, cancellationToken);
        var list = packages
            .Where(p => p.IsOnSale(now))
            .Select(p => EventPackageMapper.ToDto(p, now))
            .ToList();
        return new ApiResponse<List<EventParkingPackageDto>>(true, null, list);
    }
}

internal sealed class GetOnSaleEventVenuesHandler
    : IQueryHandler<GetOnSaleEventVenuesQuery, ApiResponse<List<EventVenueOnSaleDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetOnSaleEventVenuesHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<List<EventVenueOnSaleDto>>> HandleAsync(
        GetOnSaleEventVenuesQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        // Over-fetch packages then group (each venue may have several zones)
        var takePackages = Math.Clamp(query.Take * 4, 20, 200);
        var packages = (await _unitOfWork.EventParkingPackages.GetOnSaleAsync(now, takePackages, cancellationToken))
            .Where(p => p.IsOnSale(now))
            .ToList();

        var groups = packages
            .GroupBy(p => p.VenueEventId)
            .OrderBy(g => g.Min(p => p.EventStartUtc))
            .Take(Math.Clamp(query.Take, 1, 100))
            .Select(g =>
            {
                var zones = g.Select(p => EventPackageMapper.ToDto(p, now)).OrderBy(z => z.PackagePrice).ToList();
                var first = g.OrderBy(p => p.EventStartUtc).First();
                return new EventVenueOnSaleDto(
                    g.Key,
                    first.EventName ?? first.Title,
                    first.VenueName,
                    first.EventStartUtc,
                    first.EventEndUtc,
                    zones.Count,
                    zones.Sum(z => z.AvailableSpots),
                    zones.Min(z => z.PackagePrice),
                    zones.Max(z => z.PackagePrice),
                    zones);
            })
            .ToList();

        return new ApiResponse<List<EventVenueOnSaleDto>>(true, null, groups);
    }
}

internal sealed class GetEventPackagesByVenueEventHandler
    : IQueryHandler<GetEventPackagesByVenueEventQuery, ApiResponse<List<EventParkingPackageDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetEventPackagesByVenueEventHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<List<EventParkingPackageDto>>> HandleAsync(
        GetEventPackagesByVenueEventQuery query,
        CancellationToken cancellationToken = default)
    {
        var packages = await _unitOfWork.EventParkingPackages.GetByVenueEventIdAsync(
            query.VenueEventId,
            query.ActiveOnly,
            cancellationToken);
        var now = DateTime.UtcNow;
        var list = packages.Select(p => EventPackageMapper.ToDto(p, now)).ToList();
        return new ApiResponse<List<EventParkingPackageDto>>(true, null, list);
    }
}

internal sealed class GetEventPackageByIdHandler
    : IQueryHandler<GetEventPackageByIdQuery, ApiResponse<EventParkingPackageDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetEventPackageByIdHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<EventParkingPackageDto>> HandleAsync(
        GetEventPackageByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var package = await _unitOfWork.EventParkingPackages.GetByIdWithSpaceAsync(query.PackageId, cancellationToken);
        if (package is null)
            return new ApiResponse<EventParkingPackageDto>(false, "Event package not found", null);

        return new ApiResponse<EventParkingPackageDto>(true, null, EventPackageMapper.ToDto(package));
    }
}

internal sealed class GetVendorEventPackagesHandler
    : IQueryHandler<GetVendorEventPackagesQuery, ApiResponse<List<EventParkingPackageDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetVendorEventPackagesHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<List<EventParkingPackageDto>>> HandleAsync(
        GetVendorEventPackagesQuery query,
        CancellationToken cancellationToken = default)
    {
        var spaces = (await _unitOfWork.ParkingSpaces.GetByOwnerIdAsync(query.VendorId, cancellationToken)).ToList();
        var spaceIds = spaces.Select(s => s.Id).ToList();
        var packages = await _unitOfWork.EventParkingPackages.GetByParkingSpaceIdsAsync(
            spaceIds,
            activeOnly: false,
            cancellationToken);

        var spaceById = spaces.ToDictionary(s => s.Id);
        var now = DateTime.UtcNow;
        var list = packages
            .Select(package =>
            {
                var dto = EventPackageMapper.ToDto(package, now);
                if (spaceById.TryGetValue(package.ParkingSpaceId, out var space))
                {
                    dto = dto with
                    {
                        ParkingSpaceTitle = space.Title,
                        ParkingSpaceAddress = space.Address,
                        ParkingSpaceCity = space.City
                    };
                }

                return dto;
            })
            .OrderBy(p => p.EventStartUtc)
            .ToList();

        return new ApiResponse<List<EventParkingPackageDto>>(true, null, list);
    }
}

internal sealed class GetVendorEventPackageAnalyticsHandler
    : IQueryHandler<GetVendorEventPackageAnalyticsQuery, ApiResponse<List<EventVenueAnalyticsDto>>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetVendorEventPackageAnalyticsHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<List<EventVenueAnalyticsDto>>> HandleAsync(
        GetVendorEventPackageAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var spaces = (await _unitOfWork.ParkingSpaces.GetByOwnerIdAsync(query.VendorId, cancellationToken)).ToList();
        var packages = await _unitOfWork.EventParkingPackages.GetByParkingSpaceIdsAsync(
            spaces.Select(s => s.Id),
            activeOnly: false,
            cancellationToken);

        if (packages.Count == 0)
            return new ApiResponse<List<EventVenueAnalyticsDto>>(true, null, new List<EventVenueAnalyticsDto>());

        var bookings = await _unitOfWork.Bookings.GetByEventPackageIdsAsync(
            packages.Select(p => p.Id),
            cancellationToken);
        var bookingsByPackage = bookings
            .Where(b => b.EventParkingPackageId.HasValue)
            .GroupBy(b => b.EventParkingPackageId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Entities.Booking>)g.ToList());

        var spaceById = spaces.ToDictionary(s => s.Id);
        var now = DateTime.UtcNow;

        var packageAnalytics = packages.Select(p =>
        {
            // Ensure parking title on package nav when missing
            if (p.ParkingSpace is null && spaceById.TryGetValue(p.ParkingSpaceId, out var space))
            {
                // DTO mapper uses nav; analytics uses package.ParkingSpace?.Title
            }

            bookingsByPackage.TryGetValue(p.Id, out var pkgBookings);
            pkgBookings ??= Array.Empty<Domain.Entities.Booking>();
            var analytics = EventPackageMapper.ToAnalytics(p, pkgBookings, now);
            if (spaceById.TryGetValue(p.ParkingSpaceId, out var s) &&
                string.Equals(analytics.ParkingSpaceTitle, "Parking", StringComparison.Ordinal))
            {
                analytics = analytics with { ParkingSpaceTitle = s.Title };
            }

            return analytics;
        }).ToList();

        var venues = packageAnalytics
            .GroupBy(a => a.VenueEventId)
            .Select(g =>
            {
                var pkgs = g.OrderBy(x => x.EventStartUtc).ToList();
                var first = pkgs[0];
                var totalSpots = pkgs.Sum(x => x.TotalSpots);
                var sold = pkgs.Sum(x => x.SoldCount);
                var sellThrough = totalSpots <= 0
                    ? 0m
                    : Math.Round(100m * sold / totalSpots, 1, MidpointRounding.AwayFromZero);

                return new EventVenueAnalyticsDto(
                    g.Key,
                    first.EventName,
                    first.VenueName,
                    pkgs.Min(x => x.EventStartUtc),
                    pkgs.Max(x => x.EventEndUtc),
                    pkgs.Count,
                    totalSpots,
                    sold,
                    pkgs.Sum(x => x.AvailableSpots),
                    sellThrough,
                    pkgs.Sum(x => x.GrossRevenue),
                    pkgs);
            })
            .OrderBy(v => v.EventStartUtc)
            .ToList();

        return new ApiResponse<List<EventVenueAnalyticsDto>>(true, null, venues);
    }
}

internal sealed class GetEventPackageAnalyticsHandler
    : IQueryHandler<GetEventPackageAnalyticsQuery, ApiResponse<EventPackageAnalyticsDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetEventPackageAnalyticsHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<EventPackageAnalyticsDto>> HandleAsync(
        GetEventPackageAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var package = await _unitOfWork.EventParkingPackages.GetByIdWithSpaceAsync(query.PackageId, cancellationToken);
        if (package is null)
            return new ApiResponse<EventPackageAnalyticsDto>(false, "Event package not found", null);

        var parking = package.ParkingSpace
            ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(package.ParkingSpaceId, cancellationToken);
        if (parking is null)
            return new ApiResponse<EventPackageAnalyticsDto>(false, "Parking space not found", null);
        if (!query.IsAdmin && parking.OwnerId != query.ActorUserId)
            return new ApiResponse<EventPackageAnalyticsDto>(false, "Unauthorized", null);

        var bookings = await _unitOfWork.Bookings.GetByEventPackageIdsAsync(
            new[] { package.Id },
            cancellationToken);

        var analytics = EventPackageMapper.ToAnalytics(package, bookings);
        if (string.Equals(analytics.ParkingSpaceTitle, "Parking", StringComparison.Ordinal))
            analytics = analytics with { ParkingSpaceTitle = parking.Title };

        return new ApiResponse<EventPackageAnalyticsDto>(true, null, analytics);
    }
}
