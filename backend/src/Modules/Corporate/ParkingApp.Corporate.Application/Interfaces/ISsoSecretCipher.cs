namespace ParkingApp.Corporate.Application.Interfaces;

/// <summary>
/// Protects/unprotects Corporate SSO secrets (Data Protection purpose ParkEase.CorporateSso.Secrets.v1).
/// Application-facing alias so Corporate handlers do not depend on Identity.Application.
/// </summary>
public interface ISsoSecretCipher
{
    string Protect(string plaintext);

    bool TryUnprotect(string protectedCiphertext, out string? plaintext);

    public const string SecretUnreadableCode = "SecretUnreadable";
}
