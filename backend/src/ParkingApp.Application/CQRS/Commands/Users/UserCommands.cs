using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ParkingApp.Application.CQRS.Commands.Users;

// ────────────────────────────────────────────────────────────────
// Commands & Queries
// ────────────────────────────────────────────────────────────────

public sealed record GetCurrentUserQuery(Guid UserId) : IQuery<ApiResponse<UserDto>>;
public sealed record UpdateUserCommand(Guid UserId, UpdateUserDto Dto) : ICommand<ApiResponse<UserDto>>;
public sealed record DeleteUserCommand(Guid UserId) : ICommand<ApiResponse<bool>>;

// ────────────────────────────────────────────────────────────────
// Handlers
// ────────────────────────────────────────────────────────────────

public sealed class GetCurrentUserHandler : IQueryHandler<GetCurrentUserQuery, ApiResponse<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public GetCurrentUserHandler(IUnitOfWork unitOfWork, ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<ApiResponse<UserDto>> HandleAsync(GetCurrentUserQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"user:{query.UserId}";
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

public sealed class UpdateUserHandler : ICommandHandler<UpdateUserCommand, ApiResponse<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<UpdateUserHandler> _logger;

    public UpdateUserHandler(IUnitOfWork unitOfWork, ICacheService cache, ILogger<UpdateUserHandler> logger)
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

        if (!string.IsNullOrEmpty(command.Dto.FirstName)) user.FirstName = command.Dto.FirstName.Trim();
        if (!string.IsNullOrEmpty(command.Dto.LastName)) user.LastName = command.Dto.LastName.Trim();
        if (!string.IsNullOrEmpty(command.Dto.PhoneNumber)) user.PhoneNumber = command.Dto.PhoneNumber.Trim();

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync($"user:{command.UserId}", cancellationToken);

        _logger.LogInformation("User profile updated: {UserId}", command.UserId);
        return new ApiResponse<UserDto>(true, "Profile updated", user.ToDto());
    }
}

public sealed class DeleteUserHandler : ICommandHandler<DeleteUserCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<DeleteUserHandler> _logger;

    public DeleteUserHandler(IUnitOfWork unitOfWork, ICacheService cache, ILogger<DeleteUserHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            return new ApiResponse<bool>(false, "User not found", false);

        _unitOfWork.Users.Remove(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync($"user:{command.UserId}", cancellationToken);

        _logger.LogWarning("User account deleted: {UserId}", command.UserId);
        return new ApiResponse<bool>(true, "Account deleted", true);
    }
}
