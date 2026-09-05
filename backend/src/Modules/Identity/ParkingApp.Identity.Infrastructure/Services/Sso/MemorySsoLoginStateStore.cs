using System.Collections.Concurrent;
using ParkingApp.Identity.Application.Interfaces;

namespace ParkingApp.Identity.Infrastructure.Services.Sso;

/// <summary>In-process fail-closed state store (single-node / dev). Atomic consume via TryRemove.</summary>
public sealed class MemorySsoLoginStateStore : ISsoLoginStateStore
{
    private readonly ConcurrentDictionary<string, Entry> _states = new(StringComparer.Ordinal);

    public Task StoreAsync(SsoLoginStateRecord state, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(state.StateId))
            throw new ArgumentException("StateId is required", nameof(state));
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl));

        var expires = DateTime.UtcNow.Add(ttl);
        var record = state with { ExpiresAtUtc = expires };
        _states[state.StateId] = new Entry(record, expires);
        return Task.CompletedTask;
    }

    public Task<SsoLoginStateRecord?> TryConsumeAsync(string stateId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stateId))
            return Task.FromResult<SsoLoginStateRecord?>(null);

        if (!_states.TryRemove(stateId, out var entry))
            return Task.FromResult<SsoLoginStateRecord?>(null);

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
            return Task.FromResult<SsoLoginStateRecord?>(null);

        return Task.FromResult<SsoLoginStateRecord?>(entry.Record);
    }

    private sealed record Entry(SsoLoginStateRecord Record, DateTime ExpiresAtUtc);
}
