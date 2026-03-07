using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Vehicles;

public record CreateVehicleCommand(Guid UserId, CreateVehicleDto Dto) : ICommand<ApiResponse<VehicleDto>>;

public record UpdateVehicleCommand(Guid Id, Guid UserId, UpdateVehicleDto Dto) : ICommand<ApiResponse<VehicleDto>>;

public record DeleteVehicleCommand(Guid Id, Guid UserId) : ICommand<ApiResponse<bool>>;

public class CreateVehicleCommandHandler : ICommandHandler<CreateVehicleCommand, ApiResponse<VehicleDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateVehicleCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<VehicleDto>> HandleAsync(CreateVehicleCommand request, CancellationToken cancellationToken = default)
    {
        bool isDefault = request.Dto.IsDefault;

        // If this is set as default, we need to unset any existing default vehicle
        if (isDefault)
        {
            var existingDefault = await _unitOfWork.Vehicles.GetDefaultVehicleAsync(request.UserId, cancellationToken);
            if (existingDefault != null)
            {
                existingDefault.IsDefault = false;
                _unitOfWork.Vehicles.Update(existingDefault);
            }
        }
        else
        {
            // If it's the first vehicle, make it default automatically
            var userVehicles = await _unitOfWork.Vehicles.GetByUserIdAsync(request.UserId, cancellationToken);
            if (!userVehicles.Any())
            {
                isDefault = true;
            }
        }

        // Check for duplicate license plate for this user
        var existingVehicles = await _unitOfWork.Vehicles.GetByUserIdAsync(request.UserId, cancellationToken);
        var normalizedPlate = request.Dto.LicensePlate.Trim().ToUpperInvariant();
        if (existingVehicles.Any(v => v.LicensePlate.Trim().ToUpperInvariant() == normalizedPlate))
        {
            return new ApiResponse<VehicleDto>(false, "A vehicle with this license plate already exists", null);
        }

        var vehicle = new Vehicle
        {
            UserId = request.UserId,
            LicensePlate = request.Dto.LicensePlate,
            Make = request.Dto.Make,
            Model = request.Dto.Model,
            Color = request.Dto.Color,
            Type = request.Dto.Type,
            IsDefault = isDefault
        };

        await _unitOfWork.Vehicles.AddAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<VehicleDto>(true, "Vehicle created successfully", new VehicleDto(
            vehicle.Id,
            vehicle.UserId,
            vehicle.LicensePlate,
            vehicle.Make,
            vehicle.Model,
            vehicle.Color,
            vehicle.Type,
            vehicle.IsDefault,
            vehicle.CreatedAt
        ));
    }
}

public class UpdateVehicleCommandHandler : ICommandHandler<UpdateVehicleCommand, ApiResponse<VehicleDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVehicleCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<VehicleDto>> HandleAsync(UpdateVehicleCommand request, CancellationToken cancellationToken = default)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(request.Id, cancellationToken);
        
        if (vehicle == null)
            return new ApiResponse<VehicleDto>(false, "Vehicle not found", null);
            
        if (vehicle.UserId != request.UserId)
            return new ApiResponse<VehicleDto>(false, "Unauthorized to update this vehicle", null);

        bool isDefault = request.Dto.IsDefault;

        // If this is set as default, we need to unset any existing default vehicle
        if (isDefault && !vehicle.IsDefault)
        {
            var existingDefault = await _unitOfWork.Vehicles.GetDefaultVehicleAsync(request.UserId, cancellationToken);
            if (existingDefault != null && existingDefault.Id != vehicle.Id)
            {
                existingDefault.IsDefault = false;
                _unitOfWork.Vehicles.Update(existingDefault);
            }
        }
        else if (!isDefault && vehicle.IsDefault)
        {
            // Cannot unset default if it's the only vehicle
            var userVehicles = await _unitOfWork.Vehicles.GetByUserIdAsync(request.UserId, cancellationToken);
            if (userVehicles.Count() == 1)
            {
                isDefault = true; // Force default if it's the only one
            }
        }

        // Check for duplicate license plate for this user
        var existingVehicles = await _unitOfWork.Vehicles.GetByUserIdAsync(request.UserId, cancellationToken);
        var normalizedPlate = request.Dto.LicensePlate.Trim().ToUpperInvariant();
        if (existingVehicles.Any(v => v.Id != vehicle.Id && v.LicensePlate.Trim().ToUpperInvariant() == normalizedPlate))
        {
            return new ApiResponse<VehicleDto>(false, "A vehicle with this license plate already exists", null);
        }

        vehicle.LicensePlate = request.Dto.LicensePlate;
        vehicle.Make = request.Dto.Make;
        vehicle.Model = request.Dto.Model;
        vehicle.Color = request.Dto.Color;
        vehicle.Type = request.Dto.Type;
        vehicle.IsDefault = isDefault;

        _unitOfWork.Vehicles.Update(vehicle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<VehicleDto>(true, "Vehicle updated successfully", new VehicleDto(
            vehicle.Id,
            vehicle.UserId,
            vehicle.LicensePlate,
            vehicle.Make,
            vehicle.Model,
            vehicle.Color,
            vehicle.Type,
            vehicle.IsDefault,
            vehicle.CreatedAt
        ));
    }
}

public class DeleteVehicleCommandHandler : ICommandHandler<DeleteVehicleCommand, ApiResponse<bool>>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteVehicleCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> HandleAsync(DeleteVehicleCommand request, CancellationToken cancellationToken = default)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(request.Id, cancellationToken);
        
        if (vehicle == null)
            return new ApiResponse<bool>(false, "Vehicle not found", false);
            
        if (vehicle.UserId != request.UserId)
            return new ApiResponse<bool>(false, "Unauthorized to delete this vehicle", false);

        _unitOfWork.Vehicles.Remove(vehicle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // If it was the default vehicle and they have others, make the newest one default
        if (vehicle.IsDefault)
        {
            var remainingVehicles = await _unitOfWork.Vehicles.GetByUserIdAsync(request.UserId, cancellationToken);
            var newestVehicle = remainingVehicles.FirstOrDefault();
            if (newestVehicle != null)
            {
                newestVehicle.IsDefault = true;
                _unitOfWork.Vehicles.Update(newestVehicle);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return new ApiResponse<bool>(true, "Vehicle deleted successfully", true);
    }
}
