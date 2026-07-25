using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;


namespace ParkingApp.Marketplace.Application.Interfaces;

/// <summary>
/// Read-model port for review listings (Infrastructure implements with SQL/Dapper).
/// </summary>
public interface IReviewReadStore
{
    Task<IReadOnlyList<ReviewDto>> GetByParkingSpaceAsync(Guid parkingSpaceId, CancellationToken ct = default);
}
