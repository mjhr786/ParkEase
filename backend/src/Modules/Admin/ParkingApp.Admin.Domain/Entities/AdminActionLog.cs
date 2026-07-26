namespace ParkingApp.Admin.Domain.Entities;

/// <summary>
/// Append-only audit log for platform admin actions.
/// Intentionally does NOT inherit soft-delete BaseEntity semantics.
/// </summary>
public sealed class AdminActionLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ActorUserId { get; private set; }
    public string ActorEmail { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid? ResourceId { get; private set; }
    public string? PayloadJson { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private AdminActionLog()
    {
    }

    public static AdminActionLog Create(
        Guid actorUserId,
        string actorEmail,
        string action,
        string resourceType,
        Guid? resourceId,
        string? payloadJson,
        string? ipAddress,
        string? userAgent,
        DateTime? occurredAtUtc = null)
    {
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));
        if (string.IsNullOrWhiteSpace(actorEmail))
            throw new ArgumentException("Actor email is required.", nameof(actorEmail));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type is required.", nameof(resourceType));

        return new AdminActionLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            ActorEmail = actorEmail.Trim(),
            Action = action.Trim(),
            ResourceType = resourceType.Trim(),
            ResourceId = resourceId,
            PayloadJson = payloadJson,
            IpAddress = ipAddress,
            UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent,
            OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow
        };
    }
}
