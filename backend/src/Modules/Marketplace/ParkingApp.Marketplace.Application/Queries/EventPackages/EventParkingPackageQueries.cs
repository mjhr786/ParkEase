using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.Marketplace.Application.Queries.EventPackages;

public sealed record GetEventPackagesForParkingQuery(
    Guid ParkingSpaceId,
    bool ActiveOnly = true
) : IQuery<ApiResponse<List<EventParkingPackageDto>>>;

public sealed record GetOnSaleEventPackagesQuery(
    int Take = 50
) : IQuery<ApiResponse<List<EventParkingPackageDto>>>;

public sealed record GetOnSaleEventVenuesQuery(
    int Take = 50
) : IQuery<ApiResponse<List<EventVenueOnSaleDto>>>;

public sealed record GetEventPackagesByVenueEventQuery(
    Guid VenueEventId,
    bool ActiveOnly = true
) : IQuery<ApiResponse<List<EventParkingPackageDto>>>;

public sealed record GetEventPackageByIdQuery(
    Guid PackageId
) : IQuery<ApiResponse<EventParkingPackageDto>>;

public sealed record GetVendorEventPackagesQuery(
    Guid VendorId
) : IQuery<ApiResponse<List<EventParkingPackageDto>>>;

public sealed record GetVendorEventPackageAnalyticsQuery(
    Guid VendorId
) : IQuery<ApiResponse<List<EventVenueAnalyticsDto>>>;

public sealed record GetEventPackageAnalyticsQuery(
    Guid PackageId,
    Guid ActorUserId,
    bool IsAdmin
) : IQuery<ApiResponse<EventPackageAnalyticsDto>>;
