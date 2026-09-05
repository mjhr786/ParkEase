namespace ParkingApp.Identity.Application.Interfaces;

/// <summary>
/// Fail-closed OAuth state store for Corporate SSO (KD-CS-11). Never use ICacheService.
/// SET/GET/consume failures must surface as errors (503), not silent miss.
/// </summary>
public interface ISsoLoginStateStore
{
    Task StoreAsync(SsoLoginStateRecord state, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Atomic try-consume: returns record once; subsequent calls return null (already consumed / missing).</summary>
    Task<SsoLoginStateRecord?> TryConsumeAsync(string stateId, CancellationToken cancellationToken = default);
}

public sealed record SsoLoginStateRecord(
    string StateId,
    Guid CompanyId,
    string Nonce,
    string CodeVerifier,
    string ReturnUrl,
    string ClientType,
    string? EmailHint,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);
