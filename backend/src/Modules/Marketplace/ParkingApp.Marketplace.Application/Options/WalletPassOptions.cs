namespace ParkingApp.Marketplace.Application.Options;

/// <summary>Apple / Google Wallet packaging for booking access passes (Marketplace:Wallet).</summary>
public sealed class WalletPassOptions
{
    public const string SectionName = "Marketplace:Wallet";

    /// <summary>When false, wallet endpoints report unavailable (QR pass still works).</summary>
    public bool Enabled { get; set; } = true;

    public string OrganizationName { get; set; } = "ParkEase";

    public string LogoText { get; set; } = "ParkEase";

    /// <summary>
    /// When true, serve .pkpass packages without a valid Apple PKCS#7 signature (dev/structure only).
    /// Real iOS devices will not install unsigned passes.
    /// </summary>
    public bool AllowUnsignedAppleDownload { get; set; }

    public AppleWalletOptions Apple { get; set; } = new();

    public GoogleWalletOptions Google { get; set; } = new();
}

public sealed class AppleWalletOptions
{
    /// <summary>Pass Type ID, e.g. pass.com.parkease.access</summary>
    public string PassTypeIdentifier { get; set; } = "pass.com.parkease.access";

    /// <summary>Apple Developer Team ID (10 chars).</summary>
    public string TeamIdentifier { get; set; } = string.Empty;

    /// <summary>Path to Pass Type ID certificate (.p12 / .pfx).</summary>
    public string? CertificatePath { get; set; }

    /// <summary>PFX password (prefer user-secrets / vault).</summary>
    public string? CertificatePassword { get; set; }

    /// <summary>Optional path to Apple WWDR intermediate certificate (.cer / .pem).</summary>
    public string? WwdrCertificatePath { get; set; }

    public bool HasTeamAndPassType =>
        !string.IsNullOrWhiteSpace(PassTypeIdentifier) &&
        !string.IsNullOrWhiteSpace(TeamIdentifier);

    public bool HasSigningCertificate =>
        !string.IsNullOrWhiteSpace(CertificatePath) &&
        File.Exists(CertificatePath);
}

public sealed class GoogleWalletOptions
{
    public string IssuerId { get; set; } = string.Empty;

    /// <summary>Class id suffix (e.g. parkease_access) or full issuer.class form.</summary>
    public string ClassId { get; set; } = "parkease_access";

    /// <summary>Path to Google service account JSON (private_key + client_email).</summary>
    public string? ServiceAccountJsonPath { get; set; }

    /// <summary>Optional override; otherwise taken from the JSON file.</summary>
    public string? ServiceAccountEmail { get; set; }

    public bool IsFullyConfigured =>
        !string.IsNullOrWhiteSpace(IssuerId) &&
        !string.IsNullOrWhiteSpace(ClassId) &&
        !string.IsNullOrWhiteSpace(ServiceAccountJsonPath) &&
        File.Exists(ServiceAccountJsonPath);
}
