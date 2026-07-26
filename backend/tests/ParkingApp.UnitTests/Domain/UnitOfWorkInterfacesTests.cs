using FluentAssertions;
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Messaging.Domain.Interfaces;
using Xunit;
using BbTransaction = ParkingApp.BuildingBlocks.Persistence.IUnitOfWorkTransaction;

namespace ParkingApp.UnitTests.Domain;

public class UnitOfWorkInterfacesTests
{
    [Fact]
    public void IUnitOfWork_ComposesAllContextPorts()
    {
        typeof(IUnitOfWork).Should().Implement<IMarketplaceUnitOfWork>();
        typeof(IUnitOfWork).Should().Implement<IIdentityUnitOfWork>();
        typeof(IUnitOfWork).Should().Implement<IMessagingUnitOfWork>();
        typeof(IUnitOfWork).Should().Implement<IDisposable>();
    }

    [Fact]
    public void ContextPorts_ExposeTransactionBoundary()
    {
        // Module ports use BuildingBlocks transaction boundary
        typeof(IMarketplaceUnitOfWork).Should().Implement<BbTransaction>();
        typeof(IIdentityUnitOfWork).Should().Implement<BbTransaction>();
        typeof(IMessagingUnitOfWork).Should().Implement<BbTransaction>();
        typeof(ICorporateUnitOfWork).Should().Implement<BbTransaction>();
    }

    [Fact]
    public void MarketplaceUnitOfWork_ExposesAggregateRootsOnly()
    {
        var names = typeof(IMarketplaceUnitOfWork).GetProperties().Select(p => p.Name).ToHashSet();
        names.Should().BeEquivalentTo(new[]
        {
            "ParkingSpaces", "Bookings", "ParkingPasses", "Payments", "Reviews", "Favorites",
            "LprAccessAttempts", "LprCameraKeys", "LprPlateRules",
            "EventParkingPackages", "EvChargingSessions", "ParkingAncillaryServices"
        });
    }
}





