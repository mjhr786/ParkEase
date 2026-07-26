using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.Marketplace.Application.Commands.EvCharging;

public sealed record StartEvChargingSessionCommand(
    Guid BookingId,
    string? StationId = null,
    int ConnectorId = 1,
    decimal MeterStartKwh = 0m,
    string Source = "Iot",
    Guid? ActorUserId = null,
    bool ActorIsAdmin = false
) : ICommand<ApiResponse<EvChargingSessionDto>>;

public sealed record RecordEvMeterValuesCommand(
    string OcppTransactionId,
    decimal MeterKwh,
    string Source = "Iot"
) : ICommand<ApiResponse<EvChargingSessionDto>>;

public sealed record StopEvChargingSessionCommand(
    string OcppTransactionId,
    decimal? MeterStopKwh = null,
    string Source = "Iot",
    Guid? ActorUserId = null,
    bool ActorIsAdmin = false
) : ICommand<ApiResponse<EvChargingSessionDto>>;

/// <summary>Demo path: start + meter + stop with target energy (kWh).</summary>
public sealed record SimulateEvChargingSessionCommand(
    Guid BookingId,
    decimal EnergyKwh,
    string? StationId = null,
    int ConnectorId = 1,
    Guid ActorUserId = default,
    bool ActorIsAdmin = false
) : ICommand<ApiResponse<EvChargingSessionDto>>;
