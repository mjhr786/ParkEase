namespace ParkingApp.Marketplace.Contracts;

/// <summary>
/// Marketplace module contract: other modules request parking summaries without repositories.
/// </summary>
public interface IParkingSpaceLookup
{
    Task<ParkingSpaceSummary?> GetByIdAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch lookup. Missing or inactive spaces may be omitted; callers should treat missing ids as "Unknown".
    /// </summary>
    Task<IReadOnlyList<ParkingSpaceSummary>> GetByIdsAsync(
        IReadOnlyCollection<Guid> parkingSpaceIds,
        CancellationToken cancellationToken = default);
}
