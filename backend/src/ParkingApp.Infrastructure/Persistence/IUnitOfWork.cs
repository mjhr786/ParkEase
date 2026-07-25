using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Messaging.Domain.Interfaces;

namespace ParkingApp.Infrastructure.Persistence;

/// <summary>
/// Composition-root unit of work spanning Identity, Marketplace, and Messaging module ports.
/// Lives in host Infrastructure (not Domain) so host Domain can dissolve.
/// Prefer module-scoped UoW ports in module Application handlers.
/// </summary>
public interface IUnitOfWork :
    IMarketplaceUnitOfWork,
    IIdentityUnitOfWork,
    IMessagingUnitOfWork,
    IDisposable
{
}
