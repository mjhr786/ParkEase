using System.ComponentModel.DataAnnotations;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.DTOs;

public record VehicleDto(
    Guid Id,
    Guid UserId,
    string LicensePlate,
    string Make,
    string Model,
    string Color,
    VehicleType Type,
    bool IsDefault,
    DateTime CreatedAt
);

public record CreateVehicleDto(
    [Required, StringLength(20)] string LicensePlate,
    [Required, StringLength(100)] string Make,
    [Required, StringLength(100)] string Model,
    [Required, StringLength(50)] string Color,
    [Required] VehicleType Type,
    bool IsDefault = false
);

public record UpdateVehicleDto(
    [Required, StringLength(20)] string LicensePlate,
    [Required, StringLength(100)] string Make,
    [Required, StringLength(100)] string Model,
    [Required, StringLength(50)] string Color,
    [Required] VehicleType Type,
    bool IsDefault = false
);
