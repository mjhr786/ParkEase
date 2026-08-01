using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.Favorites;

public sealed record ToggleFavoriteCommand(Guid UserId, Guid ParkingSpaceId) : ICommand<ApiResponse<bool>>;

internal sealed class ToggleFavoriteCommandHandler : ICommandHandler<ToggleFavoriteCommand, ApiResponse<bool>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public ToggleFavoriteCommandHandler(IMarketplaceUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> HandleAsync(ToggleFavoriteCommand command, CancellationToken cancellationToken = default)
    {
        var parkingSpace = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, cancellationToken);
        // KD-9: corporate-only inventory is hidden from marketplace favorites.
        if (parkingSpace == null || parkingSpace.IsCorporateOnly)
            return new ApiResponse<bool>(false, "Parking space not found", false);

        // Includes soft-deleted rows: Remove is soft-delete, and the unique index on
        // (UserId, ParkingSpaceId) still covers deleted rows, so re-favorite must restore.
        var existingFavorite = await _unitOfWork.Favorites.GetByUserAndSpaceAsync(command.UserId, command.ParkingSpaceId, cancellationToken);

        if (existingFavorite != null && !existingFavorite.IsDeleted)
        {
            // Already favorited; toggle means remove it (soft-delete)
            _unitOfWork.Favorites.Remove(existingFavorite);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<bool>(true, "Removed from favorites", false);
        }

        if (existingFavorite != null && existingFavorite.IsDeleted)
        {
            // Previously unfavorited via soft-delete; restore the existing row
            existingFavorite.IsDeleted = false;
            _unitOfWork.Favorites.Update(existingFavorite);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<bool>(true, "Added to favorites", true);
        }

        // Never favorited; insert a new row
        var favorite = new Favorite
        {
            UserId = command.UserId,
            ParkingSpaceId = command.ParkingSpaceId
        };
        await _unitOfWork.Favorites.AddAsync(favorite, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ApiResponse<bool>(true, "Added to favorites", true);
    }
}

