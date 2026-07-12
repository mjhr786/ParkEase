using Microsoft.EntityFrameworkCore;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Outbox;

public sealed class OutboxAdminStore : IOutboxAdminStore
{
    private readonly ApplicationDbContext _db;

    public OutboxAdminStore(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<OutboxMessageListResultDto> ListAsync(
        OutboxMessageStatusDto? status,
        string? typeFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.OutboxMessages.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            var mapped = MapStatus(status.Value);
            query = query.Where(m => m.Status == mapped);
        }

        if (!string.IsNullOrWhiteSpace(typeFilter))
        {
            var filter = typeFilter.Trim();
            query = query.Where(m => m.TypeName.Contains(filter) || m.IdempotencyKey.Contains(filter));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var summary = await GetSummaryAsync(cancellationToken);
        var totalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;

        return new OutboxMessageListResultDto(
            items.Select(m => ToDto(m)).ToList(),
            totalCount,
            page,
            pageSize,
            totalPages,
            summary);
    }

    public async Task<OutboxMessageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var msg = await _db.OutboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        return msg == null ? null : ToDto(msg, includeFullPayload: true);
    }

    public async Task<bool> RequeueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var msg = await _db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (msg == null)
            return false;

        // Do not requeue successfully processed messages (would risk duplicate side effects)
        if (msg.Status == OutboxStatus.Processed)
            return false;

        msg.Status = OutboxStatus.Pending;
        msg.LastError = null;
        msg.AvailableAfterUtc = DateTime.UtcNow;
        msg.ProcessedAtUtc = null;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> RequeueAllFailedAsync(CancellationToken cancellationToken = default)
    {
        var failed = await _db.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Failed)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var msg in failed)
        {
            msg.Status = OutboxStatus.Pending;
            msg.LastError = null;
            msg.AvailableAfterUtc = now;
            msg.ProcessedAtUtc = null;
        }

        if (failed.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return failed.Count;
    }

    private async Task<OutboxSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var groups = await _db.OutboxMessages.AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountOf(OutboxStatus s) => groups.FirstOrDefault(g => g.Status == s)?.Count ?? 0;

        var pending = CountOf(OutboxStatus.Pending);
        var processing = CountOf(OutboxStatus.Processing);
        var processed = CountOf(OutboxStatus.Processed);
        var failed = CountOf(OutboxStatus.Failed);

        return new OutboxSummaryDto(pending, processing, processed, failed, pending + processing + processed + failed);
    }

    private static OutboxMessageDto ToDto(OutboxMessage m, bool includeFullPayload = false)
    {
        var shortName = ShortTypeName(m.TypeName);
        var preview = includeFullPayload
            ? m.Payload
            : (m.Payload.Length <= 280 ? m.Payload : m.Payload[..280] + "…");

        return new OutboxMessageDto(
            m.Id,
            m.TypeName,
            shortName,
            m.IdempotencyKey,
            MapStatus(m.Status),
            m.AttemptCount,
            m.LastError,
            m.CreatedAtUtc,
            m.AvailableAfterUtc,
            m.ProcessedAtUtc,
            preview);
    }

    private static string ShortTypeName(string typeName)
    {
        // AssemblyQualifiedName → type name only
        var comma = typeName.IndexOf(',');
        var full = comma > 0 ? typeName[..comma] : typeName;
        var lastDot = full.LastIndexOf('.');
        return lastDot >= 0 ? full[(lastDot + 1)..] : full;
    }

    private static OutboxStatus MapStatus(OutboxMessageStatusDto status) => status switch
    {
        OutboxMessageStatusDto.Pending => OutboxStatus.Pending,
        OutboxMessageStatusDto.Processing => OutboxStatus.Processing,
        OutboxMessageStatusDto.Processed => OutboxStatus.Processed,
        OutboxMessageStatusDto.Failed => OutboxStatus.Failed,
        _ => OutboxStatus.Pending
    };

    private static OutboxMessageStatusDto MapStatus(OutboxStatus status) => status switch
    {
        OutboxStatus.Pending => OutboxMessageStatusDto.Pending,
        OutboxStatus.Processing => OutboxMessageStatusDto.Processing,
        OutboxStatus.Processed => OutboxMessageStatusDto.Processed,
        OutboxStatus.Failed => OutboxMessageStatusDto.Failed,
        _ => OutboxMessageStatusDto.Pending
    };
}
