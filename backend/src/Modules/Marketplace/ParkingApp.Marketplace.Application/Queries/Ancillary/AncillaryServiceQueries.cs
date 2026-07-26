using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.Marketplace.Application.Queries.Ancillary;

public sealed record GetAncillaryServicesForParkingQuery(
    Guid ParkingSpaceId,
    bool ActiveOnly = true
) : IQuery<ApiResponse<List<ParkingAncillaryServiceDto>>>;

public sealed record GetMyAncillaryServicesQuery(
    Guid VendorId
) : IQuery<ApiResponse<List<ParkingAncillaryServiceDto>>>;
