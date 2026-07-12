using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Read-model port for review listings (Infrastructure implements with SQL/Dapper).
/// </summary>
public interface IReviewReadStore
{
    Task<IReadOnlyList<ReviewDto>> GetByParkingSpaceAsync(Guid parkingSpaceId, CancellationToken ct = default);
}
