using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Favorites;

public sealed record ToggleFavoriteCommand(Guid UserId, Guid ParkingSpaceId) : ICommand<ApiResponse<bool>>;

public sealed class ToggleFavoriteCommandHandler : ICommandHandler<ToggleFavoriteCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;

    public ToggleFavoriteCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> HandleAsync(ToggleFavoriteCommand command, CancellationToken cancellationToken = default)
    {
        var parkingSpace = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ParkingSpaceId, cancellationToken);
        if (parkingSpace == null)
            return new ApiResponse<bool>(false, "Parking space not found", false);

        var existingFavorite = await _unitOfWork.Favorites.GetByUserAndSpaceAsync(command.UserId, command.ParkingSpaceId, cancellationToken);

        if (existingFavorite != null)
        {
            // Already favorited; toggle means remove it
            _unitOfWork.Favorites.Remove(existingFavorite);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<bool>(true, "Removed from favorites", false);
        }
        else
        {
            // Not favorited; toggle means add it
            var favorite = new Favorite
            {
                UserId = command.UserId,
                ParkingSpaceId = command.ParkingSpaceId
            };
            await _unitOfWork.Favorites.AddAsync(favorite, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<bool>(true, "Added to favorites", true); // Returns true indicating it IS now favorited
        }
    }
}
