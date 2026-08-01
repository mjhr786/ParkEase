namespace ParkingApp.BuildingBlocks.Security;

/// <summary>
/// Canonical JWT claim type constants for ParkEase product isolation.
/// </summary>
public static class ParkEaseClaimTypes
{
    public const string Channel = "channel";
    public const string CompanyId = "company_id";
    public const string CompanyRole = "company_role"; // Admin | Employee when Corporate + company bound
}
