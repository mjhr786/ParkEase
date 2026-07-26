using Microsoft.Extensions.Logging;
using ParkingApp.Marketplace.Application.Interfaces;

namespace ParkingApp.Marketplace.Infrastructure.Services;

/// <summary>
/// Software mock of an OCPP charge point — generates transaction ids, accepts start/stop.
/// Replace with a real CSMS WebSocket client in a later phase.
/// </summary>
internal sealed class MockOcppChargeStationAdapter : IOcppChargeStationAdapter
{
    private readonly ILogger<MockOcppChargeStationAdapter> _logger;

    public MockOcppChargeStationAdapter(ILogger<MockOcppChargeStationAdapter> logger)
    {
        _logger = logger;
    }

    public Task<OcppStartTransactionResult> StartTransactionAsync(
        OcppStartTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var txId = $"MOCK-{request.StationId}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Random.Shared.Next(1000, 9999)}";
        _logger.LogInformation(
            "Mock OCPP StartTransaction accepted for booking {BookingId} station {StationId} connector {ConnectorId} → {TxId}",
            request.BookingId,
            request.StationId,
            request.ConnectorId,
            txId);

        return Task.FromResult(new OcppStartTransactionResult(true, txId, "Accepted (mock)"));
    }

    public Task<OcppStopTransactionResult> StopTransactionAsync(
        string ocppTransactionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock OCPP StopTransaction accepted for {TxId}", ocppTransactionId);
        return Task.FromResult(new OcppStopTransactionResult(true, "Accepted (mock)"));
    }
}
