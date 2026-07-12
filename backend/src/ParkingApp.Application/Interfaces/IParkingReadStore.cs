using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;

namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Read-model port for marketplace parking search and map pins.
/// Implementations live in Infrastructure (EF/Dapper).
/// </summary>
public interface IParkingReadStore
{
    /// <summary>
    /// Paged marketplace search (active, non–corporate-only spaces).
    /// </summary>
    Task<IReadOnlyList<ParkingSpace>> SearchAsync(ParkingSearchDto criteria, CancellationToken ct = default);

    /// <summary>
    /// Total active parking spaces (legacy search total; not filter-scoped).
    /// </summary>
    Task<int> CountActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Lightweight map-pin projection (Dapper; max 2000 rows).
    /// </summary>
    Task<IReadOnlyList<ParkingMapDto>> GetMapPinsAsync(ParkingSearchDto criteria, CancellationToken ct = default);
}
