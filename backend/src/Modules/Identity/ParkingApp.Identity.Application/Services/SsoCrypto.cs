using System.Security.Cryptography;
using System.Text;

namespace ParkingApp.Identity.Application.Services;

/// <summary>PKCE / opaque token helpers for Corporate SSO (no Infrastructure dependency).</summary>
public static class SsoCrypto
{
    public static string CreateRandomToken(int byteCount = 32)
    {
        var data = RandomNumberGenerator.GetBytes(byteCount);
        return Base64UrlEncode(data);
    }

    public static string CreateCodeChallengeS256(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
