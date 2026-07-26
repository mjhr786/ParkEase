namespace ParkingApp.Marketplace.Contracts.Enums;

/// <summary>How EV energy is priced for a facility.</summary>
public enum EvPricingMode
{
    /// <summary>Phase 1: ceil parking hours × hourly charging rate, locked at booking create.</summary>
    Hourly = 0,

    /// <summary>Phase 2: kWh delivered × rate per kWh, settled when charge session stops.</summary>
    PerKwh = 1
}

/// <summary>Lifecycle of an EV charge session (OCPP-inspired).</summary>
public enum EvChargingSessionStatus
{
    Pending = 0,
    Charging = 1,
    Completed = 2,
    Failed = 3
}

/// <summary>Source of charge session messages.</summary>
public static class EvChargingSources
{
    public const string Iot = "Iot";
    public const string Simulator = "Simulator";
    public const string Mock = "Mock";
}
