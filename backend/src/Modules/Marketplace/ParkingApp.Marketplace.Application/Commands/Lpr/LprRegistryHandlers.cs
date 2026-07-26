using ParkingApp.Application.Common;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Application.Queries.Lpr;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.Lpr;

internal static class LprRegistryAuth
{
    public static async Task<(ParkingSpace? Parking, string? Error)> RequireOwnerOrAdminAsync(
        IMarketplaceUnitOfWork uow,
        Guid parkingSpaceId,
        Guid actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var parking = await uow.ParkingSpaces.GetByIdAsync(parkingSpaceId, cancellationToken);
        if (parking is null)
            return (null, "Parking space not found");

        if (!isAdmin && parking.OwnerId != actorUserId)
            return (null, "Unauthorized");

        return (parking, null);
    }
}

internal sealed class CreateLprCameraKeyHandler
    : ICommandHandler<CreateLprCameraKeyCommand, ApiResponse<LprCameraKeyCreatedDto>>
{
    private readonly IMarketplaceUnitOfWork _uow;

    public CreateLprCameraKeyHandler(IMarketplaceUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<LprCameraKeyCreatedDto>> HandleAsync(
        CreateLprCameraKeyCommand command,
        CancellationToken cancellationToken = default)
    {
        var (parking, error) = await LprRegistryAuth.RequireOwnerOrAdminAsync(
            _uow, command.ParkingSpaceId, command.ActorUserId, command.IsAdmin, cancellationToken);
        if (error is not null)
            return new ApiResponse<LprCameraKeyCreatedDto>(false, error, null);

        if (!parking!.IsLprEnabled)
            return new ApiResponse<LprCameraKeyCreatedDto>(
                false, "Enable LPR on this parking space before creating camera keys.", null);

        try
        {
            if (!string.IsNullOrWhiteSpace(command.KeyId)
                && await _uow.LprCameraKeys.KeyIdExistsAsync(command.KeyId.Trim(), null, cancellationToken))
            {
                return new ApiResponse<LprCameraKeyCreatedDto>(false, "Key id already exists.", null);
            }

            var (key, secret) = LprCameraKey.Create(
                command.ParkingSpaceId,
                command.Name,
                command.ActorUserId,
                command.KeyId);

            if (await _uow.LprCameraKeys.KeyIdExistsAsync(key.KeyId, null, cancellationToken))
                return new ApiResponse<LprCameraKeyCreatedDto>(false, "Key id already exists.", null);

            await _uow.LprCameraKeys.AddAsync(key, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return new ApiResponse<LprCameraKeyCreatedDto>(true, "Camera key created. Copy the secret now; it will not be shown again.",
                new LprCameraKeyCreatedDto(
                    key.Id, key.ParkingSpaceId, key.Name, key.KeyId, secret, key.SecretPrefix, key.IsEnabled, key.CreatedAt));
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<LprCameraKeyCreatedDto>(ex);
        }
    }
}

internal sealed class SetLprCameraKeyEnabledHandler
    : ICommandHandler<SetLprCameraKeyEnabledCommand, ApiResponse<LprCameraKeyDto>>
{
    private readonly IMarketplaceUnitOfWork _uow;

    public SetLprCameraKeyEnabledHandler(IMarketplaceUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<LprCameraKeyDto>> HandleAsync(
        SetLprCameraKeyEnabledCommand command,
        CancellationToken cancellationToken = default)
    {
        var (_, error) = await LprRegistryAuth.RequireOwnerOrAdminAsync(
            _uow, command.ParkingSpaceId, command.ActorUserId, command.IsAdmin, cancellationToken);
        if (error is not null)
            return new ApiResponse<LprCameraKeyDto>(false, error, null);

        var key = await _uow.LprCameraKeys.GetByIdAsync(command.CameraKeyId, cancellationToken);
        if (key is null || key.ParkingSpaceId != command.ParkingSpaceId)
            return new ApiResponse<LprCameraKeyDto>(false, "Camera key not found", null);

        key.SetEnabled(command.IsEnabled);
        _uow.LprCameraKeys.Update(key);
        await _uow.SaveChangesAsync(cancellationToken);

        return new ApiResponse<LprCameraKeyDto>(true, command.IsEnabled ? "Camera key enabled" : "Camera key disabled",
            ToDto(key));
    }

    private static LprCameraKeyDto ToDto(LprCameraKey key) =>
        new(key.Id, key.ParkingSpaceId, key.Name, key.KeyId, key.SecretPrefix, key.IsEnabled, key.CreatedAt);
}

internal sealed class DeleteLprCameraKeyHandler
    : ICommandHandler<DeleteLprCameraKeyCommand, ApiResponse<bool>>
{
    private readonly IMarketplaceUnitOfWork _uow;

    public DeleteLprCameraKeyHandler(IMarketplaceUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<bool>> HandleAsync(
        DeleteLprCameraKeyCommand command,
        CancellationToken cancellationToken = default)
    {
        var (_, error) = await LprRegistryAuth.RequireOwnerOrAdminAsync(
            _uow, command.ParkingSpaceId, command.ActorUserId, command.IsAdmin, cancellationToken);
        if (error is not null)
            return new ApiResponse<bool>(false, error, false);

        var key = await _uow.LprCameraKeys.GetByIdAsync(command.CameraKeyId, cancellationToken);
        if (key is null || key.ParkingSpaceId != command.ParkingSpaceId)
            return new ApiResponse<bool>(false, "Camera key not found", false);

        _uow.LprCameraKeys.Remove(key);
        await _uow.SaveChangesAsync(cancellationToken);
        return new ApiResponse<bool>(true, "Camera key deleted", true);
    }
}

internal sealed class CreateLprPlateRuleHandler
    : ICommandHandler<CreateLprPlateRuleCommand, ApiResponse<LprPlateRuleDto>>
{
    private readonly IMarketplaceUnitOfWork _uow;

    public CreateLprPlateRuleHandler(IMarketplaceUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<LprPlateRuleDto>> HandleAsync(
        CreateLprPlateRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var (parking, error) = await LprRegistryAuth.RequireOwnerOrAdminAsync(
            _uow, command.ParkingSpaceId, command.ActorUserId, command.IsAdmin, cancellationToken);
        if (error is not null)
            return new ApiResponse<LprPlateRuleDto>(false, error, null);

        if (!parking!.IsLprEnabled)
            return new ApiResponse<LprPlateRuleDto>(
                false, "Enable LPR on this parking space before adding plate rules.", null);

        try
        {
            var rule = LprPlateRule.Create(
                command.ParkingSpaceId,
                command.LicensePlate,
                command.RuleType,
                command.ActorUserId,
                command.Note);

            if (await _uow.LprPlateRules.ExistsAsync(
                    command.ParkingSpaceId, rule.LicensePlateNormalized, rule.RuleType, null, cancellationToken))
            {
                return new ApiResponse<LprPlateRuleDto>(false, "This plate rule already exists.", null);
            }

            await _uow.LprPlateRules.AddAsync(rule, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return new ApiResponse<LprPlateRuleDto>(true, "Plate rule created", ToDto(rule));
        }
        catch (DomainException ex)
        {
            return DomainExceptionMapping.ToFailureResponse<LprPlateRuleDto>(ex);
        }
    }

    private static LprPlateRuleDto ToDto(LprPlateRule rule) =>
        new(rule.Id, rule.ParkingSpaceId, rule.LicensePlateNormalized, rule.RuleType, rule.Note, rule.IsEnabled, rule.CreatedAt);
}

internal sealed class SetLprPlateRuleEnabledHandler
    : ICommandHandler<SetLprPlateRuleEnabledCommand, ApiResponse<LprPlateRuleDto>>
{
    private readonly IMarketplaceUnitOfWork _uow;

    public SetLprPlateRuleEnabledHandler(IMarketplaceUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<LprPlateRuleDto>> HandleAsync(
        SetLprPlateRuleEnabledCommand command,
        CancellationToken cancellationToken = default)
    {
        var (_, error) = await LprRegistryAuth.RequireOwnerOrAdminAsync(
            _uow, command.ParkingSpaceId, command.ActorUserId, command.IsAdmin, cancellationToken);
        if (error is not null)
            return new ApiResponse<LprPlateRuleDto>(false, error, null);

        var rule = await _uow.LprPlateRules.GetByIdAsync(command.RuleId, cancellationToken);
        if (rule is null || rule.ParkingSpaceId != command.ParkingSpaceId)
            return new ApiResponse<LprPlateRuleDto>(false, "Plate rule not found", null);

        rule.SetEnabled(command.IsEnabled);
        _uow.LprPlateRules.Update(rule);
        await _uow.SaveChangesAsync(cancellationToken);

        return new ApiResponse<LprPlateRuleDto>(true, "Plate rule updated",
            new LprPlateRuleDto(rule.Id, rule.ParkingSpaceId, rule.LicensePlateNormalized, rule.RuleType, rule.Note, rule.IsEnabled, rule.CreatedAt));
    }
}

internal sealed class DeleteLprPlateRuleHandler
    : ICommandHandler<DeleteLprPlateRuleCommand, ApiResponse<bool>>
{
    private readonly IMarketplaceUnitOfWork _uow;

    public DeleteLprPlateRuleHandler(IMarketplaceUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<bool>> HandleAsync(
        DeleteLprPlateRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var (_, error) = await LprRegistryAuth.RequireOwnerOrAdminAsync(
            _uow, command.ParkingSpaceId, command.ActorUserId, command.IsAdmin, cancellationToken);
        if (error is not null)
            return new ApiResponse<bool>(false, error, false);

        var rule = await _uow.LprPlateRules.GetByIdAsync(command.RuleId, cancellationToken);
        if (rule is null || rule.ParkingSpaceId != command.ParkingSpaceId)
            return new ApiResponse<bool>(false, "Plate rule not found", false);

        _uow.LprPlateRules.Remove(rule);
        await _uow.SaveChangesAsync(cancellationToken);
        return new ApiResponse<bool>(true, "Plate rule deleted", true);
    }
}

internal sealed class GetLprCameraKeysHandler
    : IQueryHandler<GetLprCameraKeysQuery, ApiResponse<IReadOnlyList<LprCameraKeyDto>>>
{
    private readonly IMarketplaceUnitOfWork _uow;

    public GetLprCameraKeysHandler(IMarketplaceUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<IReadOnlyList<LprCameraKeyDto>>> HandleAsync(
        GetLprCameraKeysQuery query,
        CancellationToken cancellationToken = default)
    {
        var (_, error) = await LprRegistryAuth.RequireOwnerOrAdminAsync(
            _uow, query.ParkingSpaceId, query.ActorUserId, query.IsAdmin, cancellationToken);
        if (error is not null)
            return new ApiResponse<IReadOnlyList<LprCameraKeyDto>>(false, error, null);

        var keys = await _uow.LprCameraKeys.GetByParkingSpaceIdAsync(query.ParkingSpaceId, cancellationToken);
        var dtos = keys.Select(k => new LprCameraKeyDto(
            k.Id, k.ParkingSpaceId, k.Name, k.KeyId, k.SecretPrefix, k.IsEnabled, k.CreatedAt)).ToList();

        return new ApiResponse<IReadOnlyList<LprCameraKeyDto>>(true, null, dtos);
    }
}

internal sealed class GetLprPlateRulesHandler
    : IQueryHandler<GetLprPlateRulesQuery, ApiResponse<IReadOnlyList<LprPlateRuleDto>>>
{
    private readonly IMarketplaceUnitOfWork _uow;

    public GetLprPlateRulesHandler(IMarketplaceUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<IReadOnlyList<LprPlateRuleDto>>> HandleAsync(
        GetLprPlateRulesQuery query,
        CancellationToken cancellationToken = default)
    {
        var (_, error) = await LprRegistryAuth.RequireOwnerOrAdminAsync(
            _uow, query.ParkingSpaceId, query.ActorUserId, query.IsAdmin, cancellationToken);
        if (error is not null)
            return new ApiResponse<IReadOnlyList<LprPlateRuleDto>>(false, error, null);

        var rules = await _uow.LprPlateRules.GetByParkingSpaceIdAsync(query.ParkingSpaceId, cancellationToken);
        var dtos = rules.Select(r => new LprPlateRuleDto(
            r.Id, r.ParkingSpaceId, r.LicensePlateNormalized, r.RuleType, r.Note, r.IsEnabled, r.CreatedAt)).ToList();

        return new ApiResponse<IReadOnlyList<LprPlateRuleDto>>(true, null, dtos);
    }
}
