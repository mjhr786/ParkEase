using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Events;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ApplicationDbContext _db;

    public OutboxWriter(ApplicationDbContext db)
    {
        _db = db;
    }

    public void Enqueue(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var type = domainEvent.GetType();
        var typeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
        var payload = JsonSerializer.Serialize(domainEvent, type, JsonOptions);
        var idempotencyKey = BuildIdempotencyKey(type.Name, payload, domainEvent);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TypeName = typeName,
            Payload = payload,
            IdempotencyKey = idempotencyKey,
            Status = OutboxStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            AvailableAfterUtc = DateTime.UtcNow
        });
    }

    private static string BuildIdempotencyKey(string typeName, string payload, IDomainEvent domainEvent)
    {
        // Prefer stable business keys when available via reflection (BookingId, ParkingSpaceId, …)
        var bookingId = domainEvent.GetType().GetProperty("BookingId")?.GetValue(domainEvent) as Guid?;
        if (bookingId is { } bid && bid != Guid.Empty)
            return $"{typeName}:{bid:N}";

        var parkingSpaceId = domainEvent.GetType().GetProperty("ParkingSpaceId")?.GetValue(domainEvent) as Guid?;
        var occurred = domainEvent.OccurredOn.Ticks;
        if (parkingSpaceId is { } pid && pid != Guid.Empty)
            return $"{typeName}:{pid:N}:{occurred}";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..32];
        return $"{typeName}:{hash}";
    }
}
