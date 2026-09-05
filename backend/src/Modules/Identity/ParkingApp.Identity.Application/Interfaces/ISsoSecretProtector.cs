namespace ParkingApp.Identity.Application.Interfaces;

/// <summary>
/// Protects/unprotects Corporate SSO secrets with ASP.NET Data Protection
/// purpose <c>ParkEase.CorporateSso.Secrets.v1</c> (KD-CS-12).
/// </summary>
public interface ISsoSecretProtector
{
    /// <summary>Encrypt plaintext secret. Returns ciphertext suitable for storage.</summary>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypt ciphertext. Returns false when key ring cannot unprotect (SecretUnreadable).
    /// </summary>
    bool TryUnprotect(string protectedCiphertext, out string? plaintext);

    /// <summary>Error code when unprotect fails.</summary>
    public const string SecretUnreadableCode = "SecretUnreadable";
}
