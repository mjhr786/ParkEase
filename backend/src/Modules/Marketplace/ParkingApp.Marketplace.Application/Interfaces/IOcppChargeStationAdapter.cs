namespace ParkingApp.Marketplace.Application.Interfaces;

/// <summary>
/// OCPP-inspired charge-station boundary (software pipeline).
/// Real OCPP WebSocket CSMS can replace the mock implementation later.
/// </summary>
public interface IOcppChargeStationAdapter
{
    /// <summary>Allocates a transaction id for a new charge session (RemoteStartTransaction / StartTransaction).</summary>
    Task<OcppStartTransactionResult> StartTransactionAsync(
        OcppStartTransactionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Optional: notify station of stop (RemoteStopTransaction). Mock no-ops success.</summary>
    Task<OcppStopTransactionResult> StopTransactionAsync(
        string ocppTransactionId,
        CancellationToken cancellationToken = default);
}

public sealed record OcppStartTransactionRequest(
    Guid BookingId,
    Guid ParkingSpaceId,
    string StationId,
    int ConnectorId,
    decimal MeterStartKwh
);

public sealed record OcppStartTransactionResult(
    bool Accepted,
    string? OcppTransactionId,
    string? Message
);

public sealed record OcppStopTransactionResult(
    bool Accepted,
    string? Message
);
