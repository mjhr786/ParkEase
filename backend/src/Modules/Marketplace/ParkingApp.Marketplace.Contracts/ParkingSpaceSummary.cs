namespace ParkingApp.Marketplace.Contracts;

/// <summary>
/// Cross-module marketplace parking read model. No Domain entity types.
/// OwnershipType is a string name (e.g. IndividualVendor, CompanyOwned).
/// </summary>
public sealed record ParkingSpaceSummary(
    Guid ParkingSpaceId,
    Guid OwnerId,
    string Title,
    bool IsActive,
    int TotalSpots,
    string OwnershipType,
    Guid? CompanyOwnerId = null)
{
    public bool IsCompanyOwned =>
        string.Equals(OwnershipType, "CompanyOwned", StringComparison.OrdinalIgnoreCase);
}
