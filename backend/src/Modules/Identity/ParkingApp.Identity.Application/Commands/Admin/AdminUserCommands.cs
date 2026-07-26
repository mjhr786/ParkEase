using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingApp.Admin.Contracts;
using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Identity.Domain.Enums;
using ParkingApp.Identity.Domain.Interfaces;

namespace ParkingApp.Identity.Application.Commands.Admin;

public sealed record AdminListUsersQuery(
    string? Search,
    bool? IsActive,
    int Page = 1,
    int PageSize = 25) : IQuery<ApiResponse<AdminUserPageDto>>;

public sealed record AdminGetUserQuery(Guid UserId) : IQuery<ApiResponse<AdminUserDetailDto>>;

public sealed record AdminSetUserActiveCommand(
    Guid ActorAdminUserId,
    string ActorEmail,
    Guid TargetUserId,
    bool IsActive,
    string Reason,
    string? IpAddress,
    string? UserAgent) : ICommand<ApiResponse<AdminUserDetailDto>>;

internal sealed class AdminListUsersHandler : IQueryHandler<AdminListUsersQuery, ApiResponse<AdminUserPageDto>>
{
    private readonly IUserRepository _users;

    public AdminListUsersHandler(IUserRepository users) => _users = users;

    public async Task<ApiResponse<AdminUserPageDto>> HandleAsync(
        AdminListUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 25 : Math.Min(query.PageSize, 100);

        var (items, total) = await _users.SearchForAdminAsync(
            query.Search,
            query.IsActive,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(u => new AdminUserListItemDto(
            u.Id,
            u.Email?.Value ?? string.Empty,
            u.FirstName,
            u.LastName,
            u.PhoneNumber,
            u.Role,
            u.IsActive,
            u.IsEmailVerified,
            u.LastLoginAt,
            u.CreatedAt)).ToList();

        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new ApiResponse<AdminUserPageDto>(
            true,
            null,
            new AdminUserPageDto(dtos, total, page, pageSize, totalPages));
    }
}

internal sealed class AdminGetUserHandler : IQueryHandler<AdminGetUserQuery, ApiResponse<AdminUserDetailDto>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;

    public AdminGetUserHandler(IIdentityUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<AdminUserDetailDto>> HandleAsync(
        AdminGetUserQuery query,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(query.UserId, cancellationToken);
        if (user is null)
            return new ApiResponse<AdminUserDetailDto>(false, "User not found", null);

        var vehicleCount = await _unitOfWork.Vehicles.CountAsync(
            v => v.UserId == query.UserId && !v.IsDeleted,
            cancellationToken);

        return new ApiResponse<AdminUserDetailDto>(true, null, ToDetail(user, vehicleCount));
    }

    internal static AdminUserDetailDto ToDetail(Domain.Entities.User user, int vehicleCount) =>
        new(
            user.Id,
            user.Email?.Value ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Role,
            user.IsActive,
            user.IsEmailVerified,
            user.IsPhoneVerified,
            user.LastLoginAt,
            user.CreatedAt,
            vehicleCount);
}

internal sealed class AdminSetUserActiveHandler : ICommandHandler<AdminSetUserActiveCommand, ApiResponse<AdminUserDetailDto>>
{
    private readonly IIdentityUnitOfWork _unitOfWork;
    private readonly IAdminAudit _audit;
    private readonly ICacheService _cache;
    private readonly ILogger<AdminSetUserActiveHandler> _logger;

    public AdminSetUserActiveHandler(
        IIdentityUnitOfWork unitOfWork,
        IAdminAudit audit,
        ICacheService cache,
        ILogger<AdminSetUserActiveHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<AdminUserDetailDto>> HandleAsync(
        AdminSetUserActiveCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            return new ApiResponse<AdminUserDetailDto>(false, "Reason is required", null);

        var reason = command.Reason.Trim();
        if (reason.Length > 500)
            return new ApiResponse<AdminUserDetailDto>(false, "Reason must be at most 500 characters", null);

        var user = await _unitOfWork.Users.GetByIdAsync(command.TargetUserId, cancellationToken);
        if (user is null)
            return new ApiResponse<AdminUserDetailDto>(false, "User not found", null);

        if (user.IsActive == command.IsActive)
        {
            var vehicleCount = await _unitOfWork.Vehicles.CountAsync(
                v => v.UserId == user.Id && !v.IsDeleted,
                cancellationToken);
            return new ApiResponse<AdminUserDetailDto>(
                true,
                command.IsActive ? "User is already active" : "User is already inactive",
                AdminGetUserHandler.ToDetail(user, vehicleCount));
        }

        if (!command.IsActive && user.Role == UserRole.Admin && user.IsActive)
        {
            var otherActiveAdmins = await _unitOfWork.Users.CountAsync(
                u => u.Role == UserRole.Admin && u.IsActive && u.Id != user.Id,
                cancellationToken);
            if (otherActiveAdmins == 0)
                return new ApiResponse<AdminUserDetailDto>(
                    false,
                    "Cannot deactivate the last active Admin",
                    null);
        }

        var previous = user.IsActive;
        if (command.IsActive)
            user.Activate();
        else
            user.Deactivate();

        _unitOfWork.Users.Update(user);

        var action = command.IsActive ? "User.Activate" : "User.Deactivate";
        _audit.Stage(new AdminAuditEntry(
            command.ActorAdminUserId,
            command.ActorEmail,
            action,
            "User",
            user.Id,
            JsonSerializer.Serialize(new { reason, previousActive = previous, newActive = command.IsActive }),
            command.IpAddress,
            command.UserAgent));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await CacheInvalidation.ForUserChangeAsync(_cache, user.Id, cancellationToken);

        _logger.LogInformation(
            "Admin {ActorId} set user {UserId} active={Active}. Reason: {Reason}",
            command.ActorAdminUserId,
            user.Id,
            command.IsActive,
            reason);

        var vehicles = await _unitOfWork.Vehicles.CountAsync(
            v => v.UserId == user.Id && !v.IsDeleted,
            cancellationToken);

        return new ApiResponse<AdminUserDetailDto>(
            true,
            command.IsActive ? "User activated" : "User deactivated",
            AdminGetUserHandler.ToDetail(user, vehicles));
    }
}
