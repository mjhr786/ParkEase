using ParkingApp.Marketplace.Contracts;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Infrastructure.ModuleAdapters;

/// <summary>
/// Marketplace adapter: maps ParkingSpace aggregate to contract summary.
/// </summary>
internal sealed class ParkingSpaceLookup : IParkingSpaceLookup
{
    private readonly IParkingSpaceRepository _parkingSpaces;

    public ParkingSpaceLookup(IParkingSpaceRepository parkingSpaces) => _parkingSpaces = parkingSpaces;

    public async Task<ParkingSpaceSummary?> GetByIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default)
    {
        var space = await _parkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        return space is null ? null : Map(space);
    }

    public async Task<IReadOnlyList<ParkingSpaceSummary>> GetByIdsAsync(
        IReadOnlyCollection<Guid> parkingSpaceIds,
        CancellationToken cancellationToken = default)
    {
        if (parkingSpaceIds.Count == 0)
            return Array.Empty<ParkingSpaceSummary>();

        var idSet = parkingSpaceIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (idSet.Count == 0)
            return Array.Empty<ParkingSpaceSummary>();

        var spaces = await _parkingSpaces.FindAsync(
            p => idSet.Contains(p.Id),
            cancellationToken);

        return spaces.Select(Map).ToList();
    }

    private static ParkingSpaceSummary Map(ParkingApp.Marketplace.Domain.Entities.ParkingSpace space) =>
        new(
            space.Id,
            space.OwnerId,
            space.Title,
            space.IsActive,
            space.TotalSpots,
            space.OwnershipType.ToString(),
            space.CompanyOwnerId);
}
