using Microsoft.EntityFrameworkCore;
using ParkingApp.Admin.Domain.Entities;

namespace ParkingApp.Admin.Infrastructure.Persistence;

/// <summary>
/// Host <c>ApplicationDbContext</c> implements this so Admin can stage audit rows without referencing host types.
/// </summary>
public interface IAdminDbContext
{
    DbSet<AdminActionLog> AdminActionLogs { get; }
}
