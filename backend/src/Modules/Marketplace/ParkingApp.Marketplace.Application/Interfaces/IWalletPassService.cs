namespace ParkingApp.Marketplace.Application.Interfaces;

/// <summary>
/// Builds phone-wallet packages for a booking access-pass token (Apple .pkpass / Google save JWT).
/// Presentation only — does not mutate booking state.
/// </summary>
public interface IWalletPassService
{
    WalletAvailability GetAvailability();

    /// <summary>Self-hosted QR as data URI, or null if generation fails.</summary>
    string? BuildQrDataUrl(string accessToken, int pixels = 280);

    AppleWalletPackageResult BuildApplePkPass(WalletPassContent content);

    GoogleWalletLinkResult BuildGoogleSaveLink(WalletPassContent content);
}

/// <summary>Booking fields needed to render a wallet pass.</summary>
public sealed record WalletPassContent(
    Guid BookingId,
    string? BookingReference,
    string AccessToken,
    string ParkingSpaceTitle,
    string ParkingSpaceAddress,
    DateTime StartDateTimeUtc,
    DateTime EndDateTimeUtc,
    string? VehicleNumber
);

public sealed record WalletAvailability(
    bool Enabled,
    bool AppleWalletAvailable,
    bool GoogleWalletAvailable,
    bool AppleIsSigned,
    string? StatusMessage
);

public sealed record AppleWalletPackageResult(
    bool Success,
    byte[]? Content,
    string FileName,
    bool IsSigned,
    string? ErrorMessage
);

public sealed record GoogleWalletLinkResult(
    bool Success,
    string? SaveUrl,
    bool IsConfigured,
    string? ErrorMessage
);
