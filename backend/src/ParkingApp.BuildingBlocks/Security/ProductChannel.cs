namespace ParkingApp.BuildingBlocks.Security;

/// <summary>
/// Hard product channel encoded in JWT and bound to the user session.
/// Independent of <c>UserRole</c>.
/// </summary>
public enum ProductChannel
{
    Marketplace = 1,
    Corporate = 2,
    Admin = 3
}
