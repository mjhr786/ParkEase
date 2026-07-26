namespace ParkingApp.Marketplace.Contracts.Enums;

/// <summary>Valet vehicle retrieval lifecycle for a booking.</summary>
public enum ValetStatus
{
    None = 0,
    Requested = 1,
    /// <summary>Staff acknowledged the request and is retrieving the vehicle.</summary>
    InProgress = 2,
    /// <summary>Vehicle is ready for guest pickup.</summary>
    Ready = 3,
    Completed = 4,
    Cancelled = 5
}
