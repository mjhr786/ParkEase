namespace ParkingApp.API.Options;

/// <summary>
/// Feature flag and soak settings for <see cref="Middleware.ChannelAuthorizationMiddleware"/> (KD-5, KD-10).
/// Bound from configuration section <c>ChannelIsolation</c>.
/// </summary>
public sealed class ChannelIsolationOptions
{
    public const string SectionName = "ChannelIsolation";

    /// <summary>When false (default), middleware is a no-op.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// How to treat authenticated requests missing the <c>channel</c> claim while isolation is on.
    /// <c>Marketplace</c> for flag-on soak (documented); switch to <c>Reject</c> after cutover.
    /// </summary>
    public string TreatMissingClaimAs { get; set; } = "Marketplace";

    /// <summary>
    /// When true, Corporate tokens with <c>company_id</c> must match route <c>{companyId}</c> or <c>X-Company-Id</c> when present.
    /// </summary>
    public bool EnforceCompanyClaimMatch { get; set; } = true;

    /// <summary>When false, vendor allocation allowlist rows are ignored (deny by default-deny).</summary>
    public bool VendorAllocationAllowlistEnabled { get; set; } = true;
}
