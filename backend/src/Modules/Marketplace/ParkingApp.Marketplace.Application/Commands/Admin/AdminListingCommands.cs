using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingApp.Admin.Contracts;
using ParkingApp.Application.Caching;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Application.DTOs;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.Admin;

public sealed record AdminListListingsQuery(
    string? Search,
    bool? IsActive,
    bool? IsVerified,
    int Page = 1,
    int PageSize = 25) : IQuery<ApiResponse<AdminListingPageDto>>;

public sealed record AdminGetListingQuery(Guid ListingId) : IQuery<ApiResponse<AdminListingDetailDto>>;

public sealed record AdminSetListingActiveCommand(
    Guid ActorAdminUserId,
    string ActorEmail,
    Guid ListingId,
    bool IsActive,
    string Reason,
    string? IpAddress,
    string? UserAgent) : ICommand<ApiResponse<AdminListingDetailDto>>;

public sealed record AdminSetListingVerifiedCommand(
    Guid ActorAdminUserId,
    string ActorEmail,
    Guid ListingId,
    bool IsVerified,
    string Reason,
    string? IpAddress,
    string? UserAgent) : ICommand<ApiResponse<AdminListingDetailDto>>;

internal sealed class AdminListListingsHandler : IQueryHandler<AdminListListingsQuery, ApiResponse<AdminListingPageDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public AdminListListingsHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<AdminListingPageDto>> HandleAsync(
        AdminListListingsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 25 : Math.Min(query.PageSize, 100);

        var (items, total) = await _unitOfWork.ParkingSpaces.SearchForAdminAsync(
            query.Search,
            query.IsActive,
            query.IsVerified,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(p => new AdminListingListItemDto(
            p.Id,
            p.Title,
            p.City,
            p.State,
            p.Address,
            p.OwnerId,
            p.IsActive,
            p.IsVerified,
            p.IsCorporateOnly,
            p.HourlyRate,
            p.CreatedAt)).ToList();

        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new ApiResponse<AdminListingPageDto>(
            true,
            null,
            new AdminListingPageDto(dtos, total, page, pageSize, totalPages));
    }
}

internal sealed class AdminGetListingHandler : IQueryHandler<AdminGetListingQuery, ApiResponse<AdminListingDetailDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public AdminGetListingHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<AdminListingDetailDto>> HandleAsync(
        AdminGetListingQuery query,
        CancellationToken cancellationToken = default)
    {
        var space = await _unitOfWork.ParkingSpaces.GetByIdAsync(query.ListingId, cancellationToken);
        if (space is null)
            return new ApiResponse<AdminListingDetailDto>(false, "Listing not found", null);

        return new ApiResponse<AdminListingDetailDto>(true, null, ToDetail(space));
    }

    internal static AdminListingDetailDto ToDetail(Domain.Entities.ParkingSpace p) =>
        new(
            p.Id,
            p.Title,
            p.Description,
            p.City,
            p.State,
            p.Country,
            p.Address,
            p.PostalCode,
            p.ZoneCode,
            p.OwnerId,
            p.CompanyOwnerId,
            p.IsActive,
            p.IsVerified,
            p.IsCorporateOnly,
            p.TotalSpots,
            p.AvailableSpots,
            p.HourlyRate,
            p.DailyRate,
            p.CreatedAt);
}

internal sealed class AdminSetListingActiveHandler
    : ICommandHandler<AdminSetListingActiveCommand, ApiResponse<AdminListingDetailDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IAdminAudit _audit;
    private readonly ICacheService _cache;
    private readonly ILogger<AdminSetListingActiveHandler> _logger;

    public AdminSetListingActiveHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IAdminAudit audit,
        ICacheService cache,
        ILogger<AdminSetListingActiveHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<AdminListingDetailDto>> HandleAsync(
        AdminSetListingActiveCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateReason(command.Reason, out var reason, out var error))
            return new ApiResponse<AdminListingDetailDto>(false, error, null);

        var space = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ListingId, cancellationToken);
        if (space is null)
            return new ApiResponse<AdminListingDetailDto>(false, "Listing not found", null);

        if (space.IsActive == command.IsActive)
        {
            return new ApiResponse<AdminListingDetailDto>(
                true,
                command.IsActive ? "Listing is already active" : "Listing is already inactive",
                AdminGetListingHandler.ToDetail(space));
        }

        var previous = space.IsActive;
        if (command.IsActive)
            space.Activate();
        else
            space.Deactivate();

        _unitOfWork.ParkingSpaces.Update(space);

        var action = command.IsActive ? "Listing.Activate" : "Listing.Deactivate";
        _audit.Stage(new AdminAuditEntry(
            command.ActorAdminUserId,
            command.ActorEmail,
            action,
            "Listing",
            space.Id,
            JsonSerializer.Serialize(new { reason, previousActive = previous, newActive = command.IsActive }),
            command.IpAddress,
            command.UserAgent));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await CacheInvalidation.ForParkingMutationAsync(_cache, space.Id, space.OwnerId, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Admin {ActorId} set listing {ListingId} active={Active}. Reason: {Reason}",
            command.ActorAdminUserId,
            space.Id,
            command.IsActive,
            reason);

        return new ApiResponse<AdminListingDetailDto>(
            true,
            command.IsActive ? "Listing activated" : "Listing deactivated",
            AdminGetListingHandler.ToDetail(space));
    }

    internal static bool TryValidateReason(string? reason, out string trimmed, out string? error)
    {
        trimmed = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(reason))
        {
            error = "Reason is required";
            return false;
        }

        trimmed = reason.Trim();
        if (trimmed.Length > 500)
        {
            error = "Reason must be at most 500 characters";
            return false;
        }

        return true;
    }
}

internal sealed class AdminSetListingVerifiedHandler
    : ICommandHandler<AdminSetListingVerifiedCommand, ApiResponse<AdminListingDetailDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IAdminAudit _audit;
    private readonly ICacheService _cache;
    private readonly ILogger<AdminSetListingVerifiedHandler> _logger;

    public AdminSetListingVerifiedHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IAdminAudit audit,
        ICacheService cache,
        ILogger<AdminSetListingVerifiedHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ApiResponse<AdminListingDetailDto>> HandleAsync(
        AdminSetListingVerifiedCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!AdminSetListingActiveHandler.TryValidateReason(command.Reason, out var reason, out var error))
            return new ApiResponse<AdminListingDetailDto>(false, error, null);

        var space = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.ListingId, cancellationToken);
        if (space is null)
            return new ApiResponse<AdminListingDetailDto>(false, "Listing not found", null);

        if (space.IsVerified == command.IsVerified)
        {
            return new ApiResponse<AdminListingDetailDto>(
                true,
                command.IsVerified ? "Listing is already verified" : "Listing is already unverified",
                AdminGetListingHandler.ToDetail(space));
        }

        var previous = space.IsVerified;
        if (command.IsVerified)
            space.MarkVerified();
        else
            space.Unverify();

        _unitOfWork.ParkingSpaces.Update(space);

        var action = command.IsVerified ? "Listing.Verify" : "Listing.Unverify";
        _audit.Stage(new AdminAuditEntry(
            command.ActorAdminUserId,
            command.ActorEmail,
            action,
            "Listing",
            space.Id,
            JsonSerializer.Serialize(new { reason, previousVerified = previous, newVerified = command.IsVerified }),
            command.IpAddress,
            command.UserAgent));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await CacheInvalidation.ForParkingMutationAsync(_cache, space.Id, space.OwnerId, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Admin {ActorId} set listing {ListingId} verified={Verified}. Reason: {Reason}",
            command.ActorAdminUserId,
            space.Id,
            command.IsVerified,
            reason);

        return new ApiResponse<AdminListingDetailDto>(
            true,
            command.IsVerified ? "Listing verified" : "Listing unverified",
            AdminGetListingHandler.ToDetail(space));
    }
}
