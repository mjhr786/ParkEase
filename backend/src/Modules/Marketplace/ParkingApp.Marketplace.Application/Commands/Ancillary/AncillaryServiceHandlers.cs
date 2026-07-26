using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.Ancillary;

internal sealed class CreateParkingAncillaryServiceHandler
    : ICommandHandler<CreateParkingAncillaryServiceCommand, ApiResponse<ParkingAncillaryServiceDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public CreateParkingAncillaryServiceHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<ParkingAncillaryServiceDto>> HandleAsync(
        CreateParkingAncillaryServiceCommand command,
        CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(command.Dto.ParkingSpaceId, cancellationToken);
        if (parking is null)
            return new ApiResponse<ParkingAncillaryServiceDto>(false, "Parking space not found", null);
        if (!command.IsAdmin && parking.OwnerId != command.ActorUserId)
            return new ApiResponse<ParkingAncillaryServiceDto>(false, "Unauthorized", null);

        try
        {
            var service = ParkingAncillaryService.Create(
                command.Dto.ParkingSpaceId,
                command.Dto.Name,
                command.Dto.Price,
                command.Dto.Description,
                command.Dto.DurationMinutes,
                command.Dto.SortOrder,
                command.Dto.IsActive);

            await _unitOfWork.ParkingAncillaryServices.AddAsync(service, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ApiResponse<ParkingAncillaryServiceDto>(
                true,
                "Add-on service created",
                AncillaryServiceResolver.ToDto(service));
        }
        catch (DomainException ex)
        {
            return new ApiResponse<ParkingAncillaryServiceDto>(false, ex.Message, null);
        }
    }
}

internal sealed class UpdateParkingAncillaryServiceHandler
    : ICommandHandler<UpdateParkingAncillaryServiceCommand, ApiResponse<ParkingAncillaryServiceDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public UpdateParkingAncillaryServiceHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<ParkingAncillaryServiceDto>> HandleAsync(
        UpdateParkingAncillaryServiceCommand command,
        CancellationToken cancellationToken = default)
    {
        var service = await _unitOfWork.ParkingAncillaryServices.GetByIdWithSpaceAsync(
            command.ServiceId, cancellationToken);
        if (service is null)
            return new ApiResponse<ParkingAncillaryServiceDto>(false, "Add-on service not found", null);

        var parking = service.ParkingSpace
            ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(service.ParkingSpaceId, cancellationToken);
        if (parking is null)
            return new ApiResponse<ParkingAncillaryServiceDto>(false, "Parking space not found", null);
        if (!command.IsAdmin && parking.OwnerId != command.ActorUserId)
            return new ApiResponse<ParkingAncillaryServiceDto>(false, "Unauthorized", null);

        try
        {
            service.Update(
                command.Dto.Name,
                command.Dto.Description,
                command.Dto.Price,
                command.Dto.DurationMinutes,
                command.Dto.IsActive,
                command.Dto.SortOrder);

            _unitOfWork.ParkingAncillaryServices.Update(service);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ApiResponse<ParkingAncillaryServiceDto>(
                true,
                "Add-on service updated",
                AncillaryServiceResolver.ToDto(service));
        }
        catch (DomainException ex)
        {
            return new ApiResponse<ParkingAncillaryServiceDto>(false, ex.Message, null);
        }
    }
}

internal sealed class DeactivateParkingAncillaryServiceHandler
    : ICommandHandler<DeactivateParkingAncillaryServiceCommand, ApiResponse<bool>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public DeactivateParkingAncillaryServiceHandler(IMarketplaceUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<ApiResponse<bool>> HandleAsync(
        DeactivateParkingAncillaryServiceCommand command,
        CancellationToken cancellationToken = default)
    {
        var service = await _unitOfWork.ParkingAncillaryServices.GetByIdWithSpaceAsync(
            command.ServiceId, cancellationToken);
        if (service is null)
            return new ApiResponse<bool>(false, "Add-on service not found", false);

        var parking = service.ParkingSpace
            ?? await _unitOfWork.ParkingSpaces.GetByIdAsync(service.ParkingSpaceId, cancellationToken);
        if (parking is null)
            return new ApiResponse<bool>(false, "Parking space not found", false);
        if (!command.IsAdmin && parking.OwnerId != command.ActorUserId)
            return new ApiResponse<bool>(false, "Unauthorized", false);

        service.Deactivate();
        _unitOfWork.ParkingAncillaryServices.Update(service);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<bool>(true, "Add-on service deactivated", true);
    }
}
