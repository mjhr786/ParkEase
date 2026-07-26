namespace ParkingApp.Marketplace.Contracts.Enums;

public enum LprDirection
{
    Entry = 1,
    Exit = 2
}

public enum LprAccessDecision
{
    Granted = 1,
    Denied = 2
}

/// <summary>Stable machine-readable denial codes for IoT cameras and simulator UI.</summary>
public static class LprDenialReasonCodes
{
    public const string InvalidPlate = "InvalidPlate";
    public const string UnknownFacility = "UnknownFacility";
    public const string NoMatchingBooking = "NoMatchingBooking";
    public const string AmbiguousMatch = "AmbiguousMatch";
    public const string InvalidState = "InvalidState";
    public const string OutsideCheckInWindow = "OutsideCheckInWindow";
    public const string AlreadyCompleted = "AlreadyCompleted";
    public const string ReplayRejected = "ReplayRejected";
    public const string LprDisabled = "LprDisabled";
    public const string KeyNotAuthorizedForFacility = "KeyNotAuthorizedForFacility";
    public const string NotFacilityOwner = "NotFacilityOwner";
    public const string PlateDenied = "PlateDenied";
    public const string PlateNotAllowlisted = "PlateNotAllowlisted";
    public const string LowConfidence = "LowConfidence";
}

/// <summary>Facility plate rule: deny always blocks; allow rules restrict LPR to listed plates when any exist.</summary>
public enum LprPlateRuleType
{
    Allow = 1,
    Deny = 2
}

public static class LprAccessSources
{
    public const string Iot = "Iot";
    public const string Simulator = "Simulator";
}
