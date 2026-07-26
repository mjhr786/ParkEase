using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;

using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Outbox;

public sealed class OutboxProcessor : IOutboxProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ApplicationDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        ApplicationDbContext db,
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessor> logger)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var batch = await _db.OutboxMessages
            .Where(m =>
                (m.Status == OutboxStatus.Pending || m.Status == OutboxStatus.Failed) &&
                (m.AvailableAfterUtc == null || m.AvailableAfterUtc <= now) &&
                m.AttemptCount < 10)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(batchSize)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        return await ProcessByIdsAsync(batch, cancellationToken);
    }

    public async Task<int> ProcessByIdsAsync(IReadOnlyList<Guid> messageIds, CancellationToken cancellationToken = default)
    {
        if (messageIds == null || messageIds.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var batch = await _db.OutboxMessages
            .Where(m => messageIds.Contains(m.Id))
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var message in batch)
        {
            if (message.Status == OutboxStatus.Processed)
            {
                continue;
            }

            // Skip if already successfully processed under this idempotency key
            var alreadyDone = await _db.OutboxMessages.AnyAsync(
                m => m.IdempotencyKey == message.IdempotencyKey
                     && m.Status == OutboxStatus.Processed
                     && m.Id != message.Id,
                cancellationToken);
            if (alreadyDone)
            {
                message.Status = OutboxStatus.Processed;
                message.ProcessedAtUtc = now;
                await _db.SaveChangesAsync(cancellationToken);
                continue;
            }

            // Atomically claim the message (ExecuteUpdate is relational-only; InMemory unit tests use local claim).
            if (_db.Database.IsRelational())
            {
                var rowsAffected = await _db.OutboxMessages
                    .Where(m => m.Id == message.Id && (m.Status == OutboxStatus.Pending || m.Status == OutboxStatus.Failed))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Status, OutboxStatus.Processing)
                        .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1),
                        cancellationToken);

                if (rowsAffected == 0)
                {
                    // Another thread/process beat us to it, or it was already processed
                    continue;
                }

                // Sync the local tracked entity so subsequent SaveChangesAsync doesn't overwrite
                message.Status = OutboxStatus.Processing;
                message.AttemptCount += 1;
            }
            else
            {
                if (message.Status is not (OutboxStatus.Pending or OutboxStatus.Failed))
                    continue;

                message.Status = OutboxStatus.Processing;
                message.AttemptCount += 1;
                await _db.SaveChangesAsync(cancellationToken);
            }

            // After claim, do not cancel mid-side-effect because the HTTP request ended.
            // Otherwise the row can be retried and non-idempotent handlers fire again.
            var workToken = CancellationToken.None;

            try
            {
                await DispatchMessageAsync(message, workToken);

                message.Status = OutboxStatus.Processed;
                message.ProcessedAtUtc = DateTime.UtcNow;
                message.LastError = null;
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Outbox message {MessageId} ({Type}) failed (attempt {Attempt})",
                    message.Id,
                    message.TypeName,
                    message.AttemptCount);

                message.Status = message.AttemptCount >= 10 ? OutboxStatus.Failed : OutboxStatus.Pending;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                // Exponential backoff: 2^attempt seconds (capped)
                var delaySeconds = Math.Min(300, Math.Pow(2, Math.Min(message.AttemptCount, 8)));
                message.AvailableAfterUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
            }

            await _db.SaveChangesAsync(workToken);
        }

        return processed;
    }

    private async Task DispatchMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var eventType = Type.GetType(message.TypeName, throwOnError: false)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.FullName == message.TypeName || t.AssemblyQualifiedName == message.TypeName);

        // BuildingBlocks.IDomainEvent ΓÇö module-domain events do not implement Domain.Events.IDomainEvent
        if (eventType == null || !typeof(ParkingApp.BuildingBlocks.Domain.IDomainEvent).IsAssignableFrom(eventType))
            throw new InvalidOperationException($"Cannot resolve domain event type '{message.TypeName}'.");

        var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize outbox payload for {message.TypeName}.");

        var handlerInterfaces = new[]
        {
            typeof(IDomainEventHandler<>).MakeGenericType(eventType),
        };

        var handlers = new List<(object Handler, Type Interface)>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var iface in handlerInterfaces)
        {
            foreach (var h in _serviceProvider.GetServices(iface).Where(x => x != null)!)
            {
                if (seen.Add(h!))
                    handlers.Add((h!, iface));
            }
        }

        _logger.LogWarning("DEBUG: Resolving handlers for {Event}: Count = {Count}", eventType.Name, handlers.Count);

        if (handlers.Count == 0)
        {
            _logger.LogDebug("No handlers for outbox event {Type}", eventType.Name);
            return;
        }

        foreach (var (handler, handlerInterface) in handlers)
        {
            try
            {
                var method = handlerInterface.GetMethod("HandleAsync")
                    ?? throw new InvalidOperationException("HandleAsync not found on event handler interface.");
                // Rethrow so the outbox row stays pending for retry (unlike legacy silent dispatcher)
                var task = (Task)method.Invoke(handler, new[] { domainEvent, cancellationToken })!;
                await task;
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }
    }
}
