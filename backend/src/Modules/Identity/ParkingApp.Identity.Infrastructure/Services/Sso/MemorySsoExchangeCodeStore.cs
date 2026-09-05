using System.Collections.Concurrent;
using ParkingApp.Identity.Application.Interfaces;

namespace ParkingApp.Identity.Infrastructure.Services.Sso;

/// <summary>In-process fail-closed exchange-code store (single-node / dev).</summary>
public sealed class MemorySsoExchangeCodeStore : ISsoExchangeCodeStore
{
    private readonly ConcurrentDictionary<string, Entry> _codes = new(StringComparer.Ordinal);

    public Task StoreAsync(SsoExchangeCodeRecord record, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.Code))
            throw new ArgumentException("Code is required", nameof(record));
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl));

        var expires = DateTime.UtcNow.Add(ttl);
        var stored = record with { ExpiresAtUtc = expires };
        _codes[record.Code] = new Entry(stored, expires);
        return Task.CompletedTask;
    }

    public Task<SsoExchangeCodeRecord?> TryConsumeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Task.FromResult<SsoExchangeCodeRecord?>(null);

        if (!_codes.TryRemove(code, out var entry))
            return Task.FromResult<SsoExchangeCodeRecord?>(null);

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
            return Task.FromResult<SsoExchangeCodeRecord?>(null);

        return Task.FromResult<SsoExchangeCodeRecord?>(entry.Record);
    }

    private sealed record Entry(SsoExchangeCodeRecord Record, DateTime ExpiresAtUtc);
}
