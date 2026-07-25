using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;

namespace ParkingApp.Marketplace.Application.Interfaces;

/// <summary>
/// Read-model port for marketplace parking search and map pins.
/// Implementations live in Infrastructure (EF/Dapper).
/// </summary>
public interface IParkingReadStore
{
    /// <summary>
    /// Paged marketplace search (active, nonGÇôcorporate-only spaces).
    /// </summary>
    Task<IReadOnlyList<ParkingSpace>> SearchAsync(ParkingSearchDto criteria, CancellationToken ct = default);

    /// <summary>
    /// Total active parking spaces (legacy; not filter-scoped). Prefer <see cref="CountSearchAsync"/>.
    /// </summary>
    Task<int> CountActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Count of spaces matching the same filters as <see cref="SearchAsync"/> (excluding pagination).
    /// </summary>
    Task<int> CountSearchAsync(ParkingSearchDto criteria, CancellationToken ct = default);

    /// <summary>
    /// Lightweight map-pin projection (Dapper; max 2000 rows).
    /// </summary>
    Task<IReadOnlyList<ParkingMapDto>> GetMapPinsAsync(ParkingSearchDto criteria, CancellationToken ct = default);
}

