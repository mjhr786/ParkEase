using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.FileUpload.Shared;

internal static class ParkingFileUploadHelper
{
    public static async Task<ParkingSpace> GetOwnedParkingSpaceAsync(
        IMarketplaceUnitOfWork unitOfWork,
        Guid parkingSpaceId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var parking = await unitOfWork.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking == null || parking.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException("Unauthorized to upload files for this parking space");
        }

        return parking;
    }

    public static async Task AppendParkingImagesAsync(
        ParkingSpace parking,
        List<string> newUrls,
        IMarketplaceUnitOfWork unitOfWork,
        ICacheService cache,
        CancellationToken cancellationToken)
    {
        if (newUrls.Count == 0)
        {
            return;
        }

        parking.AppendImageUrls(newUrls);

        unitOfWork.ParkingSpaces.Update(parking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await InvalidateParkingCachesAsync(cache, parking.Id, cancellationToken);
    }

    public static async Task InvalidateParkingCachesAsync(
        ICacheService cache,
        Guid parkingSpaceId,
        CancellationToken cancellationToken)
    {
        await cache.RemoveAsync($"parking:{parkingSpaceId}", cancellationToken);
        await cache.RemoveByPatternAsync("search:*", cancellationToken);
    }
}