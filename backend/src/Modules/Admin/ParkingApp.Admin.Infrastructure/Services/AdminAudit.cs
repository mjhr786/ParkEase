using ParkingApp.Admin.Contracts;
using ParkingApp.Admin.Domain.Entities;
using ParkingApp.Admin.Infrastructure.Persistence;

namespace ParkingApp.Admin.Infrastructure.Services;

/// <summary>
/// Stages audit rows on the shared DbContext. Does not call SaveChanges.
/// </summary>
public sealed class AdminAudit : IAdminAudit
{
    private readonly IAdminDbContext _db;

    public AdminAudit(IAdminDbContext db) => _db = db;

    public void Stage(AdminAuditEntry entry)
    {
        var log = AdminActionLog.Create(
            entry.ActorUserId,
            entry.ActorEmail,
            entry.Action,
            entry.ResourceType,
            entry.ResourceId,
            entry.PayloadJson,
            entry.IpAddress,
            entry.UserAgent);

        _db.AdminActionLogs.Add(log);
    }
}
