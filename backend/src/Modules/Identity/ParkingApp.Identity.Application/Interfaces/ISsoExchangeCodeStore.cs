namespace ParkingApp.Identity.Application.Interfaces;

/// <summary>Fail-closed one-time exchange code store (post-callback → complete).</summary>
public interface ISsoExchangeCodeStore
{
    Task StoreAsync(SsoExchangeCodeRecord record, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task<SsoExchangeCodeRecord?> TryConsumeAsync(string code, CancellationToken cancellationToken = default);
}

public sealed record SsoExchangeCodeRecord(
    string Code,
    Guid UserId,
    Guid CompanyId,
    bool ForbidBootstrap,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);
