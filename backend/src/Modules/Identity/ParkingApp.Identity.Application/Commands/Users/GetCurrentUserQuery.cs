using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using UserDto = ParkingApp.Identity.Application.DTOs.UserDto;
using ParkingApp.Application.Interfaces;
using ParkingApp.Identity.Application.Mappings;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Messaging.Contracts;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Identity.Application.Commands.Users;

// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
// Commands & Queries
// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<ApiResponse<UserDto>>;
public sealed record UpdateUserCommand(Guid UserId, UpdateUserDto Dto) : ICommand<ApiResponse<UserDto>>;
public sealed record DeleteUserCommand(Guid UserId) : ICommand<ApiResponse<bool>>;

// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ
// Handlers
// ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

internal sealed class GetCurrentUserHandler : IQueryHandler<GetCurrentUserQuery, ApiResponse<UserDto>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public GetCurrentUserHandler(IIdentityUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<UserDto>> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.User(query.UserId);
        var cached = await _cache.GetAsync<UserDto>(cacheKey, cancellationToken);
        if (cached != null)
            return new ApiResponse<UserDto>(true, null, cached);

        var user = await _unitOfWork.Users.GetByIdAsync(query.UserId, cancellationToken);
        if (user == null)
            return new ApiResponse<UserDto>(false, "User not found", null);

        var dto = user.ToDto();
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);
        return new ApiResponse<UserDto>(true, null, dto);
    }
}

internal sealed class UpdateUserHandler : ICommandHandler<UpdateUserCommand, ApiResponse<UserDto>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<UpdateUserHandler> _logger;

    public UpdateUserHandler(IIdentityUnitOfWork unitOfWork, ICacheService cache, ILogger<UpdateUserHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<UserDto>> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            return new ApiResponse<UserDto>(false, "User not found", null);

        user.UpdateProfile(command.Dto.FirstName, command.Dto.LastName, command.Dto.PhoneNumber);

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await CacheInvalidation.ForUserChangeAsync(_cache, command.UserId, cancellationToken);

        _logger.LogInformation("User profile updated: {UserId}", command.UserId);
        return new ApiResponse<UserDto>(true, "Profile updated", user.ToDto());
    }
}

internal sealed class DeleteUserHandler : ICommandHandler<DeleteUserCommand, ApiResponse<bool>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly IMarketplaceUserDataCleanup _marketplaceCleanup;
    private readonly IMessagingUserDataCleanup _messagingCleanup;
    private readonly ICacheService _cache;
    private readonly ILogger<DeleteUserHandler> _logger;

    public DeleteUserHandler(
        IIdentityUnitOfWork unitOfWork,
        IMarketplaceUserDataCleanup marketplaceCleanup,
        IMessagingUserDataCleanup messagingCleanup,
        ICacheService cache,
        ILogger<DeleteUserHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _marketplaceCleanup = marketplaceCleanup;
        _messagingCleanup = messagingCleanup;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            return new ApiResponse<bool>(false, "User not found", false);

        // Shared ApplicationDbContext / UnitOfWork: one transaction covers all module facades.
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _marketplaceCleanup.StageDeleteForUserAsync(command.UserId, cancellationToken);
            await _messagingCleanup.StageDeleteForUserAsync(command.UserId, cancellationToken);

            var vehicles = await _unitOfWork.Vehicles.FindAsync(v => v.UserId == command.UserId, cancellationToken);
            _unitOfWork.Vehicles.HardDeleteRange(vehicles);

            var deviceTokens = await _unitOfWork.DeviceTokens.FindAsync(t => t.UserId == command.UserId, cancellationToken);
            _unitOfWork.DeviceTokens.HardDeleteRange(deviceTokens);

            _unitOfWork.Users.HardDelete(user);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            await Task.WhenAll(
                CacheInvalidation.ForUserChangeAsync(_cache, command.UserId, cancellationToken),
                _cache.RemoveAsync(CacheKeys.MemberDashboard(command.UserId), cancellationToken),
                _cache.RemoveAsync(CacheKeys.VendorDashboard(command.UserId), cancellationToken));

            _logger.LogWarning("User account permanently deleted: {UserId}", command.UserId);
            return new ApiResponse<bool>(true, "Account deleted", true);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
