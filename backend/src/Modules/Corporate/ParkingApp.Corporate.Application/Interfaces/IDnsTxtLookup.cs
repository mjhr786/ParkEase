namespace ParkingApp.Corporate.Application.Interfaces;

/// <summary>Public DNS TXT lookup for SSO domain verification.</summary>
public interface IDnsTxtLookup
{
    /// <summary>
    /// Returns all TXT string fragments for <paramref name="host"/> (may be empty on NXDOMAIN / timeout).
    /// Implementations must not throw for ordinary DNS failures; return empty and log.
    /// </summary>
    Task<IReadOnlyList<string>> LookupTxtAsync(string host, CancellationToken cancellationToken = default);
}
