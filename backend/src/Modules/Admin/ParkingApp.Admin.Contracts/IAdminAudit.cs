namespace ParkingApp.Admin.Contracts;

/// <summary>
/// Stage an admin audit row on the shared DbContext. Implementations MUST NOT call SaveChanges;
/// the caller's unit-of-work commits domain change + audit together.
/// </summary>
public interface IAdminAudit
{
    void Stage(AdminAuditEntry entry);
}

/// <summary>Null object for unit tests that do not assert audit side-effects.</summary>
public sealed class NullAdminAudit : IAdminAudit
{
    public static readonly NullAdminAudit Instance = new();

    public void Stage(AdminAuditEntry entry)
    {
        // no-op
    }
}

public sealed record AdminAuditEntry(
    Guid ActorUserId,
    string ActorEmail,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    string? PayloadJson,
    string? IpAddress,
    string? UserAgent);
