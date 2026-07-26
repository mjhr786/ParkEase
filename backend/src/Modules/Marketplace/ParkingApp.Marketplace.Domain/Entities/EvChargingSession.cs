using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Services;

namespace ParkingApp.Marketplace.Domain.Entities;

/// <summary>
/// OCPP-inspired EV charge session: start transaction → meter values → stop + energy fee.
/// </summary>
public class EvChargingSession : BaseEntity
{
    public Guid BookingId { get; internal set; }
    public Guid ParkingSpaceId { get; internal set; }

    /// <summary>Station / charge point id (OCPP chargePointId).</summary>
    public string StationId { get; internal set; } = "MOCK-1";

    /// <summary>Connector id on the station (OCPP connectorId).</summary>
    public int ConnectorId { get; internal set; } = 1;

    /// <summary>Unique transaction id returned by the charge-station adapter.</summary>
    public string OcppTransactionId { get; internal set; } = string.Empty;

    public EvChargingSessionStatus Status { get; internal set; } = EvChargingSessionStatus.Pending;

    public DateTime StartedAtUtc { get; internal set; }
    public DateTime? StoppedAtUtc { get; internal set; }

    /// <summary>Meter reading at start (kWh cumulative).</summary>
    public decimal MeterStartKwh { get; internal set; }

    /// <summary>Latest meter reading while charging (kWh cumulative).</summary>
    public decimal LastMeterKwh { get; internal set; }

    /// <summary>Meter reading at stop (kWh cumulative).</summary>
    public decimal? MeterEndKwh { get; internal set; }

    /// <summary>Energy delivered this session (kWh).</summary>
    public decimal EnergyDeliveredKwh { get; internal set; }

    /// <summary>Rate locked at session start (₹/kWh).</summary>
    public decimal RatePerKwh { get; internal set; }

    /// <summary>Settled energy fee after stop.</summary>
    public decimal EnergyFeeAmount { get; internal set; }

    public string Source { get; internal set; } = EvChargingSources.Mock;

    public virtual Booking? Booking { get; internal set; }

    internal EvChargingSession()
    {
    }

    public static EvChargingSession Start(
        Guid bookingId,
        Guid parkingSpaceId,
        string ocppTransactionId,
        decimal ratePerKwh,
        decimal meterStartKwh = 0m,
        string? stationId = null,
        int connectorId = 1,
        string source = EvChargingSources.Mock,
        DateTime? startedAtUtc = null)
    {
        if (bookingId == Guid.Empty)
            throw new ValidationException("bookingId", "Booking is required");
        if (parkingSpaceId == Guid.Empty)
            throw new ValidationException("parkingSpaceId", "Parking space is required");
        if (string.IsNullOrWhiteSpace(ocppTransactionId))
            throw new ValidationException("ocppTransactionId", "Transaction id is required");
        if (ratePerKwh < 0)
            throw new ValidationException("ratePerKwh", "Rate per kWh cannot be negative");
        if (meterStartKwh < 0)
            throw new ValidationException("meterStartKwh", "Meter start cannot be negative");
        if (connectorId < 1)
            throw new ValidationException("connectorId", "Connector id must be at least 1");

        var start = (startedAtUtc ?? DateTime.UtcNow).ToUniversalTime();
        var meter = Math.Round(meterStartKwh, 3, MidpointRounding.AwayFromZero);

        return new EvChargingSession
        {
            BookingId = bookingId,
            ParkingSpaceId = parkingSpaceId,
            StationId = string.IsNullOrWhiteSpace(stationId) ? "MOCK-1" : stationId.Trim(),
            ConnectorId = connectorId,
            OcppTransactionId = ocppTransactionId.Trim(),
            Status = EvChargingSessionStatus.Charging,
            StartedAtUtc = start,
            MeterStartKwh = meter,
            LastMeterKwh = meter,
            RatePerKwh = Math.Round(ratePerKwh, 2, MidpointRounding.AwayFromZero),
            Source = string.IsNullOrWhiteSpace(source) ? EvChargingSources.Mock : source.Trim()
        };
    }

    /// <summary>Records a meter value (OCPP MeterValues). Cumulative kWh must not go backwards.</summary>
    public void RecordMeterValue(decimal meterKwh, DateTime? asOfUtc = null)
    {
        if (Status != EvChargingSessionStatus.Charging)
            throw new BusinessRuleException("EvSession.Meter", $"Cannot record meter values when status is {Status}");
        if (meterKwh < 0)
            throw new ValidationException("meterKwh", "Meter value cannot be negative");

        var rounded = Math.Round(meterKwh, 3, MidpointRounding.AwayFromZero);
        if (rounded < LastMeterKwh)
            throw new BusinessRuleException("EvSession.Meter", "Meter value cannot decrease");

        LastMeterKwh = rounded;
        UpdatedAt = (asOfUtc ?? DateTime.UtcNow).ToUniversalTime();
    }

    /// <summary>
    /// Stops the session, computes energy delivered and fee.
    /// Returns the energy fee amount (0 when rate is 0 or no energy).
    /// </summary>
    public decimal Stop(decimal? meterStopKwh = null, DateTime? stoppedAtUtc = null)
    {
        if (Status == EvChargingSessionStatus.Completed)
            return EnergyFeeAmount;
        if (Status != EvChargingSessionStatus.Charging && Status != EvChargingSessionStatus.Pending)
            throw new BusinessRuleException("EvSession.Stop", $"Cannot stop session in {Status} status");

        var stopMeter = Math.Round(meterStopKwh ?? LastMeterKwh, 3, MidpointRounding.AwayFromZero);
        if (stopMeter < MeterStartKwh)
            throw new ValidationException("meterStopKwh", "Stop meter cannot be below start meter");

        MeterEndKwh = stopMeter;
        LastMeterKwh = stopMeter;
        EnergyDeliveredKwh = Math.Round(stopMeter - MeterStartKwh, 3, MidpointRounding.AwayFromZero);
        EnergyFeeAmount = EvChargingFeeCalculator.CalculateEnergyFee(EnergyDeliveredKwh, RatePerKwh);
        StoppedAtUtc = (stoppedAtUtc ?? DateTime.UtcNow).ToUniversalTime();
        Status = EvChargingSessionStatus.Completed;
        UpdatedAt = StoppedAtUtc;
        return EnergyFeeAmount;
    }

    public void MarkFailed(string? reason = null)
    {
        if (Status == EvChargingSessionStatus.Completed)
            return;
        Status = EvChargingSessionStatus.Failed;
        StoppedAtUtc = DateTime.UtcNow;
        UpdatedAt = StoppedAtUtc;
        _ = reason; // reserved for audit / events
    }
}
